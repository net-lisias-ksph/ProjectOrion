using KSP.UI.Screens;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.Localization;
namespace Orion
{
	public class ModulePulseEngine : PartModule
	{
		#region KSPFields
		//Anim stuff
		[KSPField]
		public string thrustTransformName = "thrustTransform";
		Transform thrustTransform;

		[KSPField]
		public string AnimName = "EngineAnim";
		private AnimationState AnimState;

		public float AnimSpeed;

		[KSPField]
		public string deployAnimName = "deployAnim";
		private AnimationState deployState;

		Coroutine extending;
		Coroutine retracting;

		//propellant stuff
		[KSPField]
		public string Propellant = "VYPulseUnit"; //propellant resource
		private int PropellantID;       //private int NPUID; have adjustable-yield bombs and single resource instead of tracking multiple NPU resources

		private ProtoStageIconInfo FuelGauge;

		public double NPURemaining;
		public double NPUMax;

		bool canTransferAlert = false;

		//Engine operation stuff
		Vector3 inialVelocity;

		[KSPField(isPersistant = true)]
		public bool EngineEnabled;
		[KSPField(isPersistant = true)]
		public bool enginePacked;

		public double timePulse;
		public float PulseDelay = 0; //linked to throttle setting for NPU release rate. Should be 0.87-1.0s at 100% throttle
		public float ImpulseTime = -1;

		[KSPField(guiActive = true, guiActiveEditor = false, guiName = "#LOC_SPO_Pulsedelay")]
		public string timeTillPulse;

		public bool FireNPU;
		public float ThrottlePercent;

		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_SPO_YieldSelect"),
		UI_FloatRange(minValue = 0.5f, maxValue = 5f, stepIncrement = 0.5f, scene = UI_Scene.All, affectSymCounterparts = UI_Scene.All)]
		public float yield = 1.0f; //yield in tonnes of pulse unit

		[KSPField(isPersistant = true, guiActive = true, guiName = "#LOC_SPO_Impulse", guiActiveEditor = false)]
		public string ImpulseDisplay;
		public double NPUImpulse = 4500; //Impulse per pulse unit. Have if InAtmo, everything in BlastRadius also gets impulse

		[KSPField(isPersistant = true, guiActive = true, guiName = "#LOC_SPO_BlastRadius", guiActiveEditor = false)]
		public double BlastRadius; //they're nukes. don't get close. have some sort of atmo multiplier to increase radius if in atmo

		[KSPField(isPersistant = true, guiActive = true, guiName = "#autoLOC_6001378", guiActiveEditor = false)]
		public string Ispdisplay; //Specific Impulse calc. Figure out how to feed this to the stock DV readout. Until then:

		[KSPField(isPersistant = true, guiActive = true, guiName = "#LOC_SPO_dV")]
		public string dVdisplay;

		public double Isp = 711;
		public double dV;

		public double NPUMass = 0.645; //placeholder, mass grabbed from resource def

		//[KSPField]
		//public double NPUPMass = 0.15; //mass in tons of Tungsten/Beryllium propellant. Used to determine collimation factor (% of bomb mass that hits plate as plasma)
		[KSPField]
		public double CollimationFactor = 0.2325; //use collimation factor instead - accounts for mod created NPU resources this way
		public double resourcemass;

		[KSPField]
		public double DetonationDist = 12.5;

		[KSPField]
		public double PlateDiameter = 5;

		public double atmoDensity;
		public bool hasPulseUnits;

		[KSPField]
		public bool Exhaustdamage = true; // can NPUs cause collateral damage?
		[KSPField]
		public float ExhaustDamageModifier = 1; //Used for heat damage modifier if ExhaustDamage = true

		[KSPField]
		public float KSPScalar = 7.5f; // multiplier to scale down generated impulse to levels reasonable for KSP's scale. 1 is no scaling.

		// asset paths
		[KSPField] public string AtmoBlastSFX;
		[KSPField] public string VacBlastSFX;

		[KSPField] public string OrionFX = "Orion/FX/NukeFX";

		AudioClip AtmoSFX;
		AudioClip VacSFX;
		AudioSource audioSource;

		[KSPField(isPersistant = true)]
		public bool staged;

		public static bool GameIsPaused
		{
			get { return PauseMenu.isOpen || Time.timeScale == 0; }
		}

		AttachNode bottom;
		#endregion KSPFields

		#region Action Groups

		[KSPAction("#autoLOC_6001380")] // Toggle Engine
		public void AGToggle(KSPActionParam param)
		{
			ToggleEngine();
		}
		[KSPAction("#autoLOC_6001382")] //Activate Engine
		public void AGActivate(KSPActionParam param)
		{
			ActivateEngine();
		}
		[KSPAction("#autoLOC_6001381")] //Shutdown Engine
		public void AGDeactivate(KSPActionParam param)
		{
			DeactivateEngine();
		}
		[KSPAction("#LOC_SPO_TogglePlate")] // Toggle Deployment
		public void AGPlateToggle(KSPActionParam param)
		{
			TogglePlate();
		}
		[KSPAction("#LOC_SPO_RetractPlate")] //retract Engine
		public void AGRetract(KSPActionParam param)
		{
			StopAnim();
			retracting = StartCoroutine(Retract());
		}
		[KSPAction("#LOC_SPO_ExtendPlate")] //extend Engine
		public void AGExtend(KSPActionParam param)
		{
			StopAnim();
			extending = StartCoroutine(Extend());
		}
		[KSPField(guiActive = true, guiActiveEditor = false, guiName = "#autoLOC_475347")] //Status:
		public string guiStatusString = Localizer.Format("#autoLOC_227562"); //Off

		//PartWindow buttons
		[KSPEvent(guiActive = true, guiName = "#autoLOC_6001382", active = true)] //Activate Engine
		public void ToggleEngine()
		{
			if (!EngineEnabled)
			{
				ActivateEngine();
			}
			else
			{
				DeactivateEngine();
			}
		}
		public void ActivateEngine()
		{
			if (enginePacked) // in case engine activated via AG while retracted
			{
				if (extending == null)
				{
					StopAnim();
					extending = StartCoroutine(Extend());
				}
				return;
			}
			deployState.enabled = false;
			guiStatusString = Localizer.Format("#autoLOC_219034"); //Nominal
			this.staged = true;
			Events["ToggleEngine"].guiName = Localizer.Format("#autoLOC_6001381"); //Shutdown Engine
			EngineEnabled = true;
			Events["TogglePlate"].active = false; //disable plate retract
			Events["TogglePlate"].guiActive = false;
			if (FuelGauge == null)
			{
				FuelGauge = InitFuelGauge();
			}
		}
		public void DeactivateEngine()
		{
			//Debug.Log("[Orion]: Engine Shutdown called");
			guiStatusString = Localizer.Format("#autoLOC_227562"); //Off
			EngineEnabled = false;
			this.staged = false;
			Events["ToggleEngine"].guiName = Localizer.Format("#autoLOC_6001382"); //Activate Engine
			Events["TogglePlate"].active = true; //enable plate retraction
			Events["TogglePlate"].guiActive = true;
			if (FuelGauge != null)
			{
				part.stackIcon.ClearInfoBoxes();
				FuelGauge = null;
			}// add a retract/extend plate anim for more compact designs - ie. sticking an orion drive onto of a conventional lifter as a 2nd stage?
		}
		[KSPEvent(isPersistent = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_SPO_RetractPlate", active = true)] //retract plate
		public void TogglePlate()
		{
			if (!enginePacked)
			{
				StopAnim();
				retracting = StartCoroutine(Retract());
			}
			else
			{
				StopAnim();
				extending = StartCoroutine(Extend());
			}
		}
		/////////////////////
		IEnumerator Extend()
		{
			if (bottom.FindOpposingNode() == null)
			{
				AnimState.enabled = false;
				deployState.enabled = true;
				deployState.speed = -1;
				while (deployState.normalizedTime > 0) //wait for animation here
				{
					yield return null;
				}
				deployState.normalizedTime = 0;
				deployState.speed = 0;
				bottom.nodeType = AttachNode.NodeType.Dock;
				bottom.radius = 0.001f;
				deployState.enabled = false;
				AnimState.enabled = true;
				enginePacked = false;
				Events["TogglePlate"].guiName = Localizer.Format("#LOC_SPO_RetractPlate"); //Retract Plate
				Events["ToggleEngine"].active = true;
				Events["ToggleEngine"].guiActive = true;
			}
			else
			{
				ScreenMessages.PostScreenMessage(Localizer.Format("#LOC_SPO_PlateBlocked"), 3.0f, ScreenMessageStyle.UPPER_CENTER);
			}
		}

		IEnumerator Retract()
		{
			DeactivateEngine();
			AnimState.enabled = false;
			deployState.enabled = true;
			deployState.speed = 1;
			while (deployState.normalizedTime < 1)
			{
				yield return null;
			}
			deployState.normalizedTime = 1;
			deployState.speed = 0;
			bottom.nodeType = AttachNode.NodeType.Stack;
			bottom.radius = 0.4f;
			//deployState.enabled = false;
			enginePacked = true;
			Events["TogglePlate"].guiName = Localizer.Format("#LOC_SPO_ExtendPlate"); //Extend Plate
			Events["ToggleEngine"].active = false; //disable engine activation while retracted
			Events["ToggleEngine"].guiActive = false;
		}
		void StopAnim()
		{
			if (retracting != null)
			{
				StopCoroutine(retracting);
				retracting = null;
			}
			if (extending != null)
			{
				StopCoroutine(extending);
				extending = null;
			}
		}

		///////////////////////////////
		[KSPAction("#LOC_SPO_YieldUp")]
		public void AGYieldUp(KSPActionParam param)
		{
			IncreaseYield();
		}
		[KSPAction("#LOC_SPO_YieldDown")]
		public void AGYieldDown(KSPActionParam param)
		{
			DecreaseYield();
		}
		public void IncreaseYield()
		{
			if (yield <= 4.5)
			{
				yield += 0.5f;
				ScreenMessages.PostScreenMessage((Localizer.Format("#LOC_SPO_YieldSelect") + ": "+ yield), 3.0f, ScreenMessageStyle.UPPER_CENTER);
			}
		}
		public void DecreaseYield()
		{
			if (yield >= 1.0)
			{
				yield -= 0.5f;
				ScreenMessages.PostScreenMessage((Localizer.Format("#LOC_SPO_YieldSelect") + ": " + yield), 3.0f, ScreenMessageStyle.UPPER_CENTER);
			}
		}

		#endregion Action Groups

		#region Events
		public override void OnAwake()
		{
			base.OnAwake();

			part.stagingIconAlwaysShown = true;
			this.part.stackIconGrouping = StackIconGrouping.SAME_TYPE;
		}
		public void Start()
		{
			part.stagingIconAlwaysShown = true;
			this.part.stackIconGrouping = StackIconGrouping.SAME_TYPE;
			part.stackIcon.ClearInfoBoxes();
			bottom = part.FindAttachNode("bottom");
			bottom.nodeType = AttachNode.NodeType.Dock;
			bottom.radius = 0.001f;
			FuelGauge = null;
			EngineEnabled = false;
			PropellantID = PartResourceLibrary.Instance.GetDefinition(Propellant).id;
			NPUMass = GetPulseUnits().info.density;
			if (!string.IsNullOrEmpty(deployAnimName))
			{
				deployState = SetAnimation(deployAnimName, part);
				deployState.enabled = true;
			}
			else
			{
				Actions["AGExtend"].active = false;
				Actions["AGRetract"].active = false;
				Actions["AGRetract"].active = false;
			}
			if (HighLogic.LoadedSceneIsFlight)
			{
				thrustTransform = part.FindModelTransform(thrustTransformName);
				if (enginePacked)
				{
					deployState.enabled = true;
					deployState.speed = 0;
					deployState.normalizedTime = 1;
				}
			}
			else if (HighLogic.LoadedSceneIsEditor)
			{
				thrustTransform = part.FindModelTransform(thrustTransformName);
			}

			AnimState = SetAnimation(AnimName, part);
			AnimState.enabled = false;
			vessel.ActionGroups.SetGroup(KSPActionGroup.REPLACEWITHDEFAULT, true);
			SetupAudio();
		}

		void UpdateVolume()
		{
			if (audioSource)
			{
				audioSource.volume = 2f * GameSettings.SHIP_VOLUME;
			}
		}

		void SetupAudio()
		{
			AtmoSFX = GameDatabase.Instance.GetAudioClip(AtmoBlastSFX);
			VacSFX = GameDatabase.Instance.GetAudioClip(VacBlastSFX);
			if (!audioSource)
			{
				audioSource = gameObject.AddComponent<AudioSource>();
				audioSource.bypassListenerEffects = true;
				audioSource.minDistance = .3f;
				audioSource.maxDistance = 5500;
				audioSource.priority = 10;
				audioSource.dopplerLevel = 0;
				audioSource.spatialBlend = 1;
			}
			UpdateVolume();
		}

		public static AnimationState SetAnimation(string animationName, Part part)
		{
			IEnumerator<UnityEngine.Animation> animation = part.FindModelAnimators(animationName).AsEnumerable().GetEnumerator();
			while (animation.MoveNext())
			{
				if (animation.Current == null) continue;
				AnimationState animationState = animation.Current[animationName];
				animationState.speed = 0;
				animationState.enabled = true;
				animationState.wrapMode = WrapMode.ClampForever;
				animation.Current.Blend(animationName);
				return animationState;
			}
			animation.Dispose();
			return null;
		}

		void CalculateNPUStats()
		{

				atmoDensity = vessel.atmDensity;
				//see about attaching a copy of whatever the colorchanger module used for the scorching visual effect for heatshields to any parts in line of sight affected by NPU heat to scorch them?
				//Nah, plasma temp estimated ~124000, mostly UV wavelength. Most stuff is opaque to uV, so little heat transferred to plate (is why there's Be Oxide filler - to convert X-Ray fury of detonation into Uv heat
				//isp calcs for pulse units is NPU propellant mass - a mix of Beryllium Oxide+Tungsten / NPU mass * plasma vel/G
				// Tungsten plasma vel ~150 km/s, so for 50 kg of propellant and 215 kg total mass, per-NPU Isp is ~3556. if in atmo that becomes 50kg + (1/3 Pi*plateRadius2(6.25)*offset(12.5)*1.2kg/m3 for air*atmdensity
				//atmo Isp goes up to ~10520s at sea level
				Isp = (((Math.Ceiling((15295.74f * (((((NPUMass * CollimationFactor) * (yield * 0.2f)) + ((0.3334 * Math.PI * Math.Pow(0.5 * PlateDiameter, 2) * DetonationDist) * 0.0012f * atmoDensity))) / NPUMass)) * 100) / 100)));
				//NPU mass is constant, but reducing the yield reduces the % propellant flashed into plasma before the radiation case ruptures, determining overall mass delivered to the pusherplate
				// tl;dr: lower yield bombs less efficient, have lower collimation factor
				if (!Exhaustdamage)
				{
					BlastRadius = 0;
				}
				else
				{
					BlastRadius = Math.Ceiling((((Math.Pow(yield, 0.43) + 0.3) * 1000.0)) * 100) / 100;
					//Detonation in vac is going to be 95% EMR, with a thin plasmashell of vaporized bomb components moving 10s of km/s. That would be, what, maybe a couple of kN over a spacecraft sized target at <50m range at most?
					NPUImpulse = Math.Ceiling(((((((Math.Pow((Math.Pow((Math.Pow((9.54 * Math.Pow(10.0, -3.0) * (2200.0 / DetonationDist)), 1.95)), 4.0) + Math.Pow((Math.Pow((3.01 * (1100.0 / DetonationDist)), 1.25)), 4.0)), 0.25)) //
						* 6.894) // above equation for overpressure falloff originally outputted Psi, so convert to kPa
						* atmoDensity)// accounting for atmo density
						* Math.Pow(yield, (1.0 / 3.0))) // scaling for yield
						* ((Math.PI * Math.Pow(0.5 * PlateDiameter, 2)) / KSPScalar)) //should be by surface area, but that would yield something like 130MN ASL w/ 0.5kT yield. Great if an actual multi-kiloton orion vessel, but in KSP...?
						+ (9.80665 * NPUMass * Isp)) // impulse from NPU propellant, for vac operation
						* 100) / 100; //round to 2 decimal places
				}
				//Thrust = spc.G*massflow*Isp; 9.80665*.215*3557 = 7499.68kn @ 5kt & 1599.89 kn @ 1 kt
				//Isp = ((NPUImpulse + (90 * NPUMass)) / (9.80665 * NPUMass));//account for impulse from firing the NPU for total Isp
			double drymass = (vessel.totalMass - resourcemass);
			double massfraction = (vessel.totalMass/drymass);
			dV = ((((Isp * 9.80665) * Math.Log(massfraction))) //dV from pulse unit detonation
				+(90* Math.Log(massfraction))); //Isp from NPU shot recoil; isp = ve/g: 90/G, dV is isp*G ln mass fraction, so 90 * ln massfraction
			// if adding separate NPU magazine parts for more fuel, will need dV calcs for per-stage

			Ispdisplay = (Isp.ToString("0.0") + "s");

			if (dV > 10000)
			{
				 dV = dV / 1000;
				dVdisplay = (dV.ToString("0.0") + "K m/s");
			}
			else
			{
				dVdisplay = dV.ToString("0.00 m/s");
			}

			if (NPUImpulse > 10000)
			{
				ImpulseDisplay = ((NPUImpulse / 1000).ToString("0.0") + "MN");
			}
			else
			{
				ImpulseDisplay = NPUImpulse.ToString("0.00 kN");
			}
		}
		/*void CheckClearance() this is throwing NRE spam. disabling until I can debug it
		{
			Ray ray = new Ray(thrustTransform.position, thrustTransform.forward);
			RaycastHit hit;
			KerbalEVA hitEVA = null;
			DestructibleBuilding building = null;
			if (!hitEVA)
			{
				if (Physics.Raycast(ray, out hit, 12.5f, 9076737))
				{
					Part hitPart = null;
					try
					{
						KerbalEVA eva = hit.collider.gameObject.GetComponentUpwards<KerbalEVA>();
						building = hit.collider.gameObject.GetComponentUpwards<DestructibleBuilding>();
						hitPart = eva ? eva.part : hit.collider.gameObject.GetComponentInParent<Part>();
					}
					catch (NullReferenceException)
					{
					}
					if (hitPart != null || building != null)
					{
						engineBlocked = true;
					}
					else
					{
						engineBlocked = false;
					}
				}
			}
		}*/
		void Update()
		{
			if (this.staged)
			{
				if (HighLogic.LoadedSceneIsFlight && FlightGlobals.ready && !vessel.packed && vessel.IsControllable)
				{
					if (EngineEnabled)
					{
						GetPulseUnits(); // returns NPUs in part, getConnectedResourceTotals is per vessel
						if (GetPulseUnits().amount >= 1)
						{
							hasPulseUnits = true;
							canTransferAlert = false;
							if (guiStatusString == Localizer.Format("#LOC_SPO_noFuel"))
							{
								guiStatusString = Localizer.Format("#autoLOC_219034");
							}

						}
						else
						{
							guiStatusString = Localizer.Format("#LOC_SPO_noFuel");
							hasPulseUnits = false;
							canTransferAlert = true;
						}
						vessel.GetConnectedResourceTotals(PropellantID, out double NPURemaining, out double NPUMax);
						resourcemass = (NPURemaining * NPUMass);
						if ((NPURemaining > 0 && !hasPulseUnits) && canTransferAlert)
						{
							ScreenMessages.PostScreenMessage(Localizer.Format("#LOC_SPO_TransferFuel"), 5.0f, ScreenMessageStyle.UPPER_CENTER);
							canTransferAlert = false;
						}
						if (vessel.isActiveVessel)
						{
							UpdateFuelGauge((float)(NPURemaining / NPUMax));
						}
						else
						{
							FuelGauge = null;
						}
						if (FuelGauge == null)
						{
							part.stackIcon.ClearInfoBoxes();
						}
						CalculateNPUStats();
						if (TimeWarp.WarpMode != TimeWarp.Modes.HIGH || TimeWarp.CurrentRate == 1)
						{
							if (hasPulseUnits)
							{
								ThrottlePercent = 1 - FlightGlobals.ActiveVessel.ctrlState.mainThrottle;
								if (ThrottlePercent < 1)
								{
									if (PulseDelay > (Mathf.Clamp((5 * ThrottlePercent), 1, 5)))
									{
										Mathf.Clamp(PulseDelay, 1, (5 * ThrottlePercent));
									}
									if (!GameIsPaused)
									{
										//PulseDelay -= TimeWarp.fixedDeltaTime * TimeWarp.CurrentRate;
										PulseDelay -= TimeWarp.deltaTime * TimeWarp.CurrentRate;
										if (PulseDelay <= 0)
										{
											FireEngine();
											PulseDelay = (Mathf.Clamp((5 * ThrottlePercent), 1, 5));
										}
									}
								}
								else
								{
									PulseDelay = 0.5f; // fire the engine immediatly when throttle becomes > 0. //adding a halfsec pause to ensure CalculateNPUStats has enough time to run
								}
								timeTillPulse = PulseDelay.ToString("0.00");
							}
						}
						//else
						//{
						//	ScreenMessages.PostScreenMessage("Engine Blocked", 5.0f, ScreenMessageStyle.UPPER_CENTER);
						//	FireNPU = false;
						//}
					}
					//CheckClearance();				
				}
			}
		}
		
		void FixedUpdate()
		{
			if (!this.staged && GameSettings.LAUNCH_STAGES.GetKeyDown() && this.vessel.isActiveVessel && (this.part.inverseStage == StageManager.CurrentStage - 1 || StageManager.CurrentStage == 0)) { ActivateEngine(); }
			if (this.staged)
			{
				if (EngineEnabled)
				{
					if (hasFired)
					{
						AnimDelay -= TimeWarp.fixedDeltaTime;
						if (AnimDelay <= 0)
						{
							inialVelocity = vessel.rb_velocity;
							Detonate();
							AnimSpeed = 1;
							AnimState.enabled = true;
							AnimState.normalizedTime = 0;
							AnimState.speed = AnimSpeed;
							AnimState.normalizedTime = Mathf.Repeat(AnimState.normalizedTime, 1);
							hasFired = false;
							if (TimeWarp.CurrentRate == 1)
							{
								ImpulseTime = 0.10f;
							}
							else
							{
								part.rb.AddForceAtPosition((transform.up) * (float)NPUImpulse, transform.position, ForceMode.Impulse);
								// has issues with timewarp - impulse is delivered over ~ 8 frames at TW = 1; higher timewarp reduces  frames,
							// and thus total impulse delivered - this is a problem, as dV thus varies dependant on timewarp level
							// so if timewarping, simply add instantaneous impulse. may shake fragile vessels apart; Kerbs w/ g-limits on may black out
							}
						}
					}
					
					if (TimeWarp.CurrentRate == 1)
					{
						ImpulseTime -= TimeWarp.fixedDeltaTime;
						if (ImpulseTime > 0)
						{
							double ImpulseCurve = ((Math.Sin(ImpulseTime * 10 * Math.PI) * NPUImpulse) / 3);
							part.rb.AddForceAtPosition((transform.up) * (float)ImpulseCurve, transform.position, ForceMode.Impulse);
						}
						if (ImpulseTime <= 0)
						{
							ImpulseTime = -1;
						}
					}
				}
			}
		}

		#endregion KSP Events

		#region NPU Deployment 

		public bool hasFired = false;
		double AnimDelay;
		private void FireEngine()
		{	
				part.rb.AddForceAtPosition((transform.up) * (90 * (float)NPUMass), transform.position, ForceMode.Impulse); // recoil from firing pulse charge @ 90m/s
				AnimDelay = 0.1 / TimeWarp.CurrentRate;
				hasFired = true;
				GetPulseUnits().amount--;
		}
		public PartResource GetPulseUnits()
		{
			IEnumerator<PartResource> pu = part.Resources.GetEnumerator();
			while (pu.MoveNext())
			{
				if (pu.Current == null) continue;
				if (pu.Current.resourceName == Propellant) return pu.Current;
			}
			pu.Dispose();
			return null;
		}

		void Detonate()
		{
			//affect any nearby parts/vessels that aren't the source vessel
			double blastImpulse = NPUImpulse; // distance-modified impulse from blastfront on nonvessel parts/debris/ships

			NukeFX.CreateExplosion(thrustTransform.position, (float)BlastRadius, (float)yield, (float)atmoDensity, OrionFX, inialVelocity, thrustTransform.up);

			if (atmoDensity >0) // air to boost the explosion
			{
				audioSource.PlayOneShot(AtmoSFX);
				audioSource.volume = GameSettings.SHIP_VOLUME * 4f;
				using (var blastHits = Physics.OverlapSphere(transform.position, (float)BlastRadius, 9076737).AsEnumerable().GetEnumerator())
				{
					while (blastHits.MoveNext())
					{
						if (blastHits.Current == null) continue;
						try
						{
							Part partHit = blastHits.Current.GetComponentInParent<Part>();
							if (partHit != null && partHit.mass > 0)
							{
								if (Exhaustdamage)
								{
									Rigidbody rb = partHit.Rigidbody;
									Vector3 distToG0 = thrustTransform.position - partHit.transform.position;
									if (partHit.vessel != this.vessel)
									//if (partHit != this.part) Don't want this, causes lag as entire vessel has to be re-checked every pusle. Just assume the avg vessel is going to be within the blast shadow of the pusher plate
									{
										blastImpulse = ((((((((Math.Pow((Math.Pow((Math.Pow((9.54 * Math.Pow(10.0, -3.0) * (2200.0 / distToG0.magnitude)), 1.95)), 4.0) + Math.Pow((Math.Pow((3.01 * (1100.0 / distToG0.magnitude)), 1.25)), 4.0)), 0.25)) * 6.894)
										* atmoDensity) * Math.Pow(yield, (1.0 / 3.0))))) * ExhaustDamageModifier)) * (partHit.radiativeArea / 3.0);
										partHit.skinTemperature +=((((yield * 337000000) / (4 * Math.PI * Math.Pow(distToG0.magnitude, 2.0)))*(partHit.radiativeArea / 3.0)) * atmoDensity); // Fluence scales linearly w/ yield, 1 Kt will produce between 33 TJ and 337 kJ at 0-1000m,
									} // everything gets heated via atmosphere

									Ray LoSRay = new Ray(thrustTransform.position, partHit.transform.position - thrustTransform.position);
									RaycastHit hit;
									if (Physics.Raycast(LoSRay, out hit, distToG0.magnitude, 9076737)) // only add impulse to parts with line of sight to detonation
									{
										KerbalEVA eva = hit.collider.gameObject.GetComponentUpwards<KerbalEVA>();
										Part p = eva ? eva.part : hit.collider.gameObject.GetComponentInParent<Part>();
										if (p == partHit)
										{
											if (rb == null) return;
											if (p.vessel != this.vessel)
											//if (p != this.part)
											{
												p.rb.AddForceAtPosition((partHit.transform.position - thrustTransform.position).normalized * (float)blastImpulse, partHit.transform.position, ForceMode.Impulse);
												//rb.AddExplosionForce((float)NPUImpulse, thrustTransform.position, (float)BlastRadius, 0, ForceMode.Impulse);
												
											}
										}
									}
								}
							}
							else
							{
								if (Exhaustdamage)
								{
									DestructibleBuilding building = blastHits.Current.GetComponentInParent<DestructibleBuilding>();

									if (building != null)
									{
										Vector3 distToEpicenter = thrustTransform.position - building.transform.position;
										blastImpulse = (((((Math.Pow((Math.Pow((Math.Pow((9.54 * Math.Pow(10.0, -3.0) * (2200.0 / distToEpicenter.magnitude)), 1.95)), 4.0) + Math.Pow((Math.Pow((3.01 * (1100.0 / distToEpicenter.magnitude)), 1.25)), 4.0)), 0.25)) * 6.894)
									* (atmoDensity)) * Math.Pow(yield, (1.0 / 3.0)))) * ExhaustDamageModifier;
									}
									if (blastImpulse > 140)
									{
										building.Demolish();
									}
								}
							}
						}
						catch
						{
						}

					}
				}
			}
			else //exoatmo detonation
			{
				audioSource.PlayOneShot(VacSFX);
				using (var blastHits = Physics.OverlapSphere(transform.position, (float)BlastRadius, 9076737).AsEnumerable().GetEnumerator())
				{
					while (blastHits.MoveNext())
					{
						if (blastHits.Current == null) continue;
						try
						{
							Part partHit = blastHits.Current.GetComponentInParent<Part>();
							if (partHit != null && partHit.mass > 0)
							{
								if (Exhaustdamage)
								{
									Rigidbody rb = partHit.Rigidbody;
									Vector3 distToG0 = thrustTransform.position - partHit.transform.position;
									if (partHit.vessel != this.vessel)
									{
										blastImpulse = ((((yield * 10) / (4 * Math.PI * Math.Pow(distToG0.magnitude, 2.0))) * (partHit.radiativeArea / 3.0)) * ExhaustDamageModifier);
									}
									Ray LoSRay = new Ray(thrustTransform.position, partHit.transform.position - thrustTransform.position);
									RaycastHit hit;
									if (Physics.Raycast(LoSRay, out hit, distToG0.magnitude, 9076737)) // only add heat to parts with line of sight to detonation
									{
										KerbalEVA eva = hit.collider.gameObject.GetComponentUpwards<KerbalEVA>();
										Part p = eva ? eva.part : hit.collider.gameObject.GetComponentInParent<Part>();
										if (p == partHit)
										{
											if (p.vessel != this.vessel)
											//if (p != this.part)
											{
												partHit.skinTemperature +=((((yield * 33700000) / (4 * Math.PI * Math.Pow(distToG0.magnitude, 2.0)))* (partHit.radiativeArea / 2.0))* ExhaustDamageModifier); // Fluence scales linearly w/ yield, 1 Kt will produce between 33 TJ and 337 kJ at 0-1000m
												if (rb == null) return;
												p.rb.AddForceAtPosition((partHit.transform.position - thrustTransform.position).normalized * (float)blastImpulse, partHit.transform.position, ForceMode.Impulse);
												//rb.AddExplosionForce((float)blastImpulse, thrustTransform.position, (float)BlastRadius);
											}
										}
									}
								}
							}
							else
							{
								if (Exhaustdamage)
								{
									DestructibleBuilding building = blastHits.Current.GetComponentInParent<DestructibleBuilding>();

									if (building != null)
									{
										Vector3 distToEpicenter = thrustTransform.position - building.transform.position;
										if (distToEpicenter.magnitude < (yield * 10 * ExhaustDamageModifier))
										{
											building.Demolish();
										}
									}
								}
							}
						}
						catch
						{
						}

					}
				}
			}
		}

		#endregion

		#region Updates

		void UpdateFuelGauge(float resourceamount)
		{
			if (EngineEnabled && vessel.isActiveVessel)
			{
				FuelGauge?.SetValue(resourceamount, 0, 1);
			}
			else
			{
				if (FuelGauge != null)
				{
					part.stackIcon.ClearInfoBoxes();
					FuelGauge = null;
				}
			}
		}		

		private ProtoStageIconInfo InitFuelGauge()
		{

			if (string.IsNullOrEmpty(part.stagingIcon))
			{
				part.stagingIcon = "SOLID_BOOSTER";
				part.stackIcon.CreateIcon();
				part.stagingIconAlwaysShown = true;
				part.stackIconGrouping = StackIconGrouping.SAME_TYPE;
				part.stackIcon.ClearInfoBoxes();
				FuelGauge = null;
			}
			ProtoStageIconInfo v = part.stackIcon.DisplayInfo();

			if (v != null)
			{
				v.SetMsgBgColor(XKCDColors.FrogGreen);
				v.SetMsgTextColor(XKCDColors.LightGreen);
				v.SetMessage(Localizer.Format("#LOC_SPO_PulseUnits"));
				v.SetProgressBarBgColor(XKCDColors.FrogGreen);
				v.SetProgressBarColor(XKCDColors.LightLimeGreen);
			}
			return v;
		}

		#endregion Updates

		#region part Info

		public override string GetInfo()
		{
			StringBuilder output = new StringBuilder();
			output.Append(Environment.NewLine);
			output.AppendLine(Localizer.Format("#LOC_SPO_PUY") +$": {yield}");
			output.AppendLine(Localizer.Format("#LOC_SPO_MinYield") +": 0.5 kT");
			output.AppendLine(Localizer.Format("#LOC_SPO_MaxYield") +": 5.0 kT");
			output.AppendLine(Localizer.Format("#LOC_SPO_BlastRadius") +$": {BlastRadius} m");
			output.AppendLine(Localizer.Format("#LOC_SPO_VacImpulse") +$": {Isp} @ 1kT");

			return output.ToString();
		}
		#endregion Part Info
	}
}

	
