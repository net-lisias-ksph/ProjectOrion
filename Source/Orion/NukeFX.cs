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
		private bool emittersOff = false;

		private void OnEnable()
		{
			StartTime = Time.time;
			LifeTime = Mathf.Clamp(Atmosphere, 0.04f, 1) * 5;
			PEmitters = gameObject.GetComponentsInChildren<KSPParticleEmitter>();
			using (var pe = PEmitters.AsEnumerable().GetEnumerator())
				while (pe.MoveNext())
				{
					if (pe.Current == null) continue;
					EffectBehaviour.AddParticleEmitter(pe.Current);
					pe.Current.emit = true;
					pe.Current.maxEnergy = Mathf.Clamp(Atmosphere, 0.01f, 1) * 7;
					pe.Current.minEnergy = Mathf.Clamp(Atmosphere, 0.01f, 1) * 5;
					pe.Current.maxSize = 400 * (((Yield / 2) + 1) * Mathf.Clamp(Atmosphere, 0.2f, 1));
					pe.Current.minSize = 400 * (((Yield / 2) + 1) * Mathf.Clamp(Atmosphere, 0.2f, 1));
				}

			LightFx = gameObject.GetComponent<Light>();
			LightFx.color = XKCDColors.Ecru;
			LightFx.intensity = 3;
			LightFx.range = 100 * Yield;
			LightFx.shadows = LightShadows.None;
			emittersOff = false;
			transform.position = Position;

		}
		void OnDisable()
		{
			foreach (var pe in PEmitters)
				if (pe != null)
					pe.emit = false;
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
					LightFx.intensity = 0;
				}
			}

			if (TimeIndex > 0.2f && !emittersOff)
			{
				IEnumerator<KSPParticleEmitter> pe = PEmitters.AsEnumerable().GetEnumerator();
				while (pe.MoveNext())
				{
					if (pe.Current == null) continue;
					pe.Current.emit = false;
					EffectBehaviour.RemoveParticleEmitter(pe.Current);
					emittersOff = true;
				}
				pe.Dispose();

			}
			else if (TimeIndex > LifeTime)
			{
				gameObject.SetActive(false);
				return;
			}
		}

		public void FixedUpdate()
		{
			if (TimeIndex >= 0.04 && Atmosphere < 0.05f)
			{
				transform.position = Position;
				//transform.position -= Direction * 20 * TimeWarp.fixedDeltaTime; // need a way to attach the FX to the transform to get it to travel w/ the vessel to make sure FX spawns where it needs to, but then be subject to world movement, not vessel movement, so vessel can "move away from the explosion"
			} // do a hack with the FX having a local vel or force?
			else
			{

				if (!FloatingOrigin.Offset.IsZero() || !Krakensbane.GetFrameVelocity().IsZero())
				{
					transform.position -= FloatingOrigin.OffsetNonKrakensbane;
				}
			}
		}
	}
}
