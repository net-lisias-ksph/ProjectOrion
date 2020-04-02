using System;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;

namespace Orion
{
	class PulseCharge : MonoBehaviour
	{
		/*
		public Transform spawnTransform;
		public Vessel sourceVessel;
		public double BlastRadius;
		public double NPUImpulse;
		public double yield;
		public bool Exhaustdamage;
		public float ExhaustDamageModifier;
		public float atmoDensity;
		public string AtmoSFX;
		public string AtmoBlastFX;
		public string VacSFX;
		public string VacBlastFX;
		public Rigidbody parentRB;

		Vector3 prevPosition;
		Vector3 currPosition;
		Vector3 startPosition;

		Rigidbody rb;

		void Start()
		{
			rb = gameObject.AddComponent<Rigidbody>();

			prevPosition = transform.position;
			currPosition = transform.position;
			startPosition = transform.position;

			transform.rotation = spawnTransform.rotation;
			transform.position = spawnTransform.position;
			rb.velocity = parentRB.velocity + Krakensbane.GetFrameVelocityV3f();
			rb.mass = 0.1f;
			rb.isKinematic = true;
			rb.useGravity = false;
		}

		void FixedUpdate()
		{
			if (!FloatingOrigin.Offset.IsZero() || !Krakensbane.GetFrameVelocity().IsZero())
			{
				transform.position -= FloatingOrigin.OffsetNonKrakensbane;
				prevPosition -= FloatingOrigin.OffsetNonKrakensbane;
			}

			float distanceFromStart = Vector3.Distance(transform.position, spawnTransform.position);

				if (transform.parent != null && parentRB)
				{
					transform.parent = null;
					rb.isKinematic = false;
					rb.AddRelativeForce(new Vector3(0, 0, 1800));
				}

			if (distanceFromStart > 12.5)
			{
				Detonate(transform.position);
			}
			if (FlightGlobals.getAltitudeAtPos(currPosition) <= 0)
			{
				Detonate(currPosition);
			}
			prevPosition = currPosition;		
		}
		
		void Detonate(Vector3 pos)
		{
			//affect any nearby parts/vessels that aren't the source vessel
			if (atmoDensity > 0) // there air to boost the explosion?
			{
				NukeFX.CreateExplosion(pos, (float)BlastRadius, (float)yield,true,AtmoBlastFX, AtmoSFX);
				NukeFX.CreateExplosion(pos, (float)BlastRadius, (float)yield, false, VacBlastFX, VacSFX);
				Debug.Log("Creating NukeFX");
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
									Vector3 distToG0 = currPosition - partHit.transform.position;
									if (partHit.vessel != sourceVessel)
									{
										NPUImpulse = ((((((Math.Pow((Math.Pow((Math.Pow((9.54 * Math.Pow(10.0, -3.0) * (2200.0 / distToG0.magnitude)), 1.95)), 4.0) + Math.Pow((Math.Pow((3.01 * (1100.0 / distToG0.magnitude)), 1.25)), 4.0)), 0.25)) * 6.894)
										* atmoDensity) * Math.Pow(yield, (1.0 / 3.0)))) / (partHit.mass * 1000)) * ExhaustDamageModifier / partHit.mass;
										partHit.AddSkinThermalFlux(((yield * 337000000) / (4 * Math.PI * Math.Pow(distToG0.magnitude, 2.0))) * atmoDensity); // Fluence scales linearly w/ yield, 1 Kt will produce between 33 TJ and 337 kJ at 0-1000m, assuming 50% efficiency
									}

									Ray LoSRay = new Ray(currPosition, partHit.transform.position - currPosition);
									RaycastHit hit;
									if (Physics.Raycast(LoSRay, out hit, distToG0.magnitude, 9076737)) // only add heat to parts with line of sight to detonation
									{
										KerbalEVA eva = hit.collider.gameObject.GetComponentUpwards<KerbalEVA>();
										Part p = eva ? eva.part : hit.collider.gameObject.GetComponentInParent<Part>();
										if (p == partHit)
										{
											if (p.vessel != sourceVessel)
											{
												if (rb == null) return;
												rb.AddForceAtPosition((partHit.transform.position - currPosition).normalized * (float)NPUImpulse, partHit.transform.position, ForceMode.VelocityChange);
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
										Vector3 distToEpicenter = currPosition - building.transform.position;
										NPUImpulse = (((((Math.Pow((Math.Pow((Math.Pow((9.54 * Math.Pow(10.0, -3.0) * (2200.0 / distToEpicenter.magnitude)), 1.95)), 4.0) + Math.Pow((Math.Pow((3.01 * (1100.0 / distToEpicenter.magnitude)), 1.25)), 4.0)), 0.25)) * 6.894)
									* (atmoDensity)) * Math.Pow(yield, (1.0 / 3.0)))) * ExhaustDamageModifier;
									}
									if (NPUImpulse > 140)
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
				NukeFX.CreateExplosion(pos, (float)BlastRadius,(float)yield,false,VacBlastFX, VacSFX);
						
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
									Vector3 distToG0 = currPosition - partHit.transform.position;
									if (partHit.vessel != sourceVessel)
									{
										NPUImpulse = 10 * ExhaustDamageModifier;
									}
									Ray LoSRay = new Ray(currPosition, partHit.transform.position - currPosition);
									RaycastHit hit;
									if (Physics.Raycast(LoSRay, out hit, distToG0.magnitude, 9076737)) // only add heat to parts with line of sight to detonation
									{
										KerbalEVA eva = hit.collider.gameObject.GetComponentUpwards<KerbalEVA>();
										Part p = eva ? eva.part : hit.collider.gameObject.GetComponentInParent<Part>();
										if (p == partHit)
										{
											if (p.vessel != sourceVessel)
											{
												partHit.AddSkinThermalFlux((yield * 33700000) / (4 * Math.PI * Math.Pow(distToG0.magnitude, 2.0))); // Fluence scales linearly w/ yield, 1 Kt will produce between 33 TJ and 337 kJ at 0-1000m
												if (rb == null) return;
												rb.AddForceAtPosition((partHit.transform.position + partHit.rb.velocity - currPosition).normalized * (float)NPUImpulse, partHit.transform.position + partHit.rb.velocity, ForceMode.VelocityChange);
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
										Vector3 distToEpicenter = currPosition - building.transform.position;
										if (distToEpicenter.magnitude < ((yield * 10) * ExhaustDamageModifier));
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
			Destroy(gameObject); 
		}*/
	}
}
