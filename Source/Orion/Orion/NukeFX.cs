using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Orion
{
	class NukeFX : MonoBehaviour
	{
		public KSPParticleEmitter[] PEmitters { get; set; }
		public Light LightFx { get; set; }
		public float StartTime { get; set; }
		public float BlastRadius { get; set; }
		public float Yield { get; set; }
		public Vector3 Position { get; set; }
		public Vector3 Direction { get; set; }
		public float Atmosphere { get; set; }
		public float TimeIndex => Time.time - StartTime;
		private float LifeTime { get; set; }
		private float particlesMaxEnergy;
		public Vector3 parentvelocity { get; set; }

		public static Queue<NukeFX> ExplosionsLoaded = new Queue<NukeFX>();

		private void Start()
		{
			ExplosionsLoaded.Enqueue(this);
			StartTime = Time.time;
			LifeTime = Mathf.Clamp(Atmosphere, 0.04f, 1)*5;
			PEmitters = gameObject.GetComponentsInChildren<KSPParticleEmitter>();
			IEnumerator<KSPParticleEmitter> pe = PEmitters.AsEnumerable().GetEnumerator();
			while (pe.MoveNext())
			{
				if (pe.Current == null) continue;
				EffectBehaviour.AddParticleEmitter(pe.Current);
				pe.Current.emit = true;
				pe.Current.maxEnergy = LifeTime;
				pe.Current.minEnergy = LifeTime;
				if (pe.Current.maxEnergy > particlesMaxEnergy)
				{
					particlesMaxEnergy = pe.Current.maxEnergy;
				}
			}
			pe.Dispose();

			LightFx = gameObject.AddComponent<Light>();
			LightFx.color = XKCDColors.Ecru;
			LightFx.intensity = 1;
			LightFx.range = 200;
			LightFx.shadows = LightShadows.None;

			transform.position = Position;
			//transform.position -= (parentvelocity + Krakensbane.GetFrameVelocityV3f()) * Time.fixedDeltaTime;

		}

		public void Update()
		{

			if (Atmosphere >= 0.25)
			{
				LightFx.range -= 20 * Time.deltaTime;
				LightFx.intensity -= 1 * Time.deltaTime;
			}
			else
			{
				if (TimeIndex > 0.05)
				{
					LightFx.range -= 1300 * Time.deltaTime;
					LightFx.intensity  = 0;
				}
			}

			if (TimeIndex > 0.2f)
			{
				IEnumerator<KSPParticleEmitter> pe = PEmitters.AsEnumerable().GetEnumerator();
				while (pe.MoveNext())
				{
					if (pe.Current == null) continue;
					pe.Current.emit = false;
					EffectBehaviour.RemoveParticleEmitter(pe.Current);
				}
				pe.Dispose();
			}
			
			else if (TimeIndex > LifeTime)
			{
				ExplosionsLoaded.Dequeue();
				Destroy(gameObject);
				Destroy(this);
				return;
			}
		}

		public void FixedUpdate()
		{
			if (TimeIndex >= 0.04 && Atmosphere < 0.05f)
			{
				transform.position = Position; // need a way to attach the FX to the transform to get it to travel w/ the vessel to make sure FX spawns where it needs to, but then be subject to world movement, not vessel movement, so vessel can "move away from the explosion"
			} // do a hack with the FX having a local vel or force?
			else
			{

				if (!FloatingOrigin.Offset.IsZero() || !Krakensbane.GetFrameVelocity().IsZero())
				{
					transform.position -= FloatingOrigin.OffsetNonKrakensbane;
				}
			}
		}

		public static void CreateExplosion(Vector3 position, float BlastRadius, float yield, float atmosphere, string explModelPath, Vector3 initvel, Vector3 direction = default(Vector3))
		{
			//if (ExplosionsLoaded.Count > 20) return; // dequeue doesn't remove loaded items from the count, even over reverts to launch
			var go = GameDatabase.Instance.GetModel(explModelPath);

			Quaternion rotation;
			{
				rotation = Quaternion.LookRotation(direction);
			}

			GameObject newExplosion = (GameObject)Instantiate(go, position, rotation);
			NukeFX eFx = newExplosion.AddComponent<NukeFX>();
			eFx.BlastRadius = BlastRadius;
			eFx.Position = position;
			eFx.Yield = yield;
			eFx.Direction = direction;
			eFx.Atmosphere = atmosphere;
			eFx.parentvelocity = initvel;
			newExplosion.SetActive(true);
			IEnumerator<KSPParticleEmitter> pe = newExplosion.GetComponentsInChildren<KSPParticleEmitter>().Cast<KSPParticleEmitter>()
				.GetEnumerator();
			while (pe.MoveNext())
			{
				if (pe.Current == null) continue;
				pe.Current.emit = true;
				pe.Current.maxSize *= (0.8f * (1 + (yield / 20))) * Mathf.Clamp(atmosphere, 0.125f, 1);
				pe.Current.minSize *= (0.8f * (1 + (yield / 20))) * Mathf.Clamp(atmosphere, 0.125f, 1);

				pe.Current.maxEnergy =  Mathf.Clamp(atmosphere, 0.01f, 1)*5;
				pe.Current.minEnergy =  Mathf.Clamp(atmosphere, 0.01f, 1)*5;
			}
			pe.Dispose();
		}
	}
}
