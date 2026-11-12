using System;
using System.Runtime.CompilerServices;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppRootMotion.Dynamics;
using UnityEngine;

namespace CabbySteps;

/// <summary>
/// Nate's ragdoll (the game's PuppetMaster and its muscles, one rigidbody each), for trips to
/// saved locations: holding him still while a trip waits, wiping what the ragdoll remembers of how
/// it was moving, the ground under his saved feet, and how he is, for the log.
/// <para>
/// Why the motion it remembers matters: the game's teleport switches Nate off (his whole body's
/// object, Dudest, inactive; the puppet asked to go Disabled) and back on where it puts him. Coming
/// back on, the puppet goes from Disabled to Active (<c>PuppetMaster.DisabledToActive</c>, called
/// straight away by <c>FinishCutscening</c>), which sets every part's speed to the speed its
/// animated target had when last seen (<c>Muscle.mappedVelocity</c>, <c>mappedAngularVelocity</c>);
/// and going limp (<c>AliveToDead</c>) does the same. Last seen was before the trip, so a Nate
/// tumbling or falling when he set off came back with that speed, and the game's own record of
/// his hips' and torso's motion (<c>PlayerMovement.hipVel</c> and the like) was as stale. Trips
/// taken mid-fall, and long trips where he waited seconds for the world to load (the ground he
/// stood on can unload meanwhile), threw him down on arrival.
/// </para>
/// </summary>
public static class Ragdoll
{
	private static int _mask;

	/// <summary>
	/// Hold Nate still: every part of him stopped (speed and spin zero). Returns how fast the
	/// fastest part was going (m/s) just before, or -1 if he isn't there.
	/// </summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static float HoldStill()
	{
		Il2CppReferenceArray<Muscle> muscles = Muscles(PlayerMovement.me);
		if (ReferenceEquals(muscles, null))
		{
			return -1f;
		}
		float fastest = 0f;
		Vector3 zero = default;
		for (int i = 0; i < muscles.Length; i++)
		{
			Muscle muscle = muscles[i];
			Rigidbody body = ReferenceEquals(muscle, null) ? null : muscle._rigidbody_k__BackingField;
			if (ReferenceEquals(body, null))
			{
				continue;
			}
			Vector3 v = body.velocity;
			float speed = Fast.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
			if (speed > fastest)
			{
				fastest = speed;
			}
			body.velocity = zero;
			body.angularVelocity = zero;
		}
		return fastest;
	}

	/// <summary>The teleport has switched Nate off: his body's object is inactive, or his puppet has been asked to go Disabled.</summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static bool SwitchedOff()
	{
		PlayerMovement pm = PlayerMovement.me;
		if (ReferenceEquals(pm, null))
		{
			return false;
		}
		PuppetMaster puppet = pm.pm;
		if (!ReferenceEquals(puppet, null) && puppet.mode == PuppetMaster.Mode.Disabled)
		{
			return true;
		}
		return !pm.gameObject.activeSelf;
	}

	/// <summary>
	/// Wipe what the ragdoll remembers of its motion: every part still, and the speeds the puppet
	/// keeps for switching on or going limp forgotten, its targets taken as they are now
	/// (<c>Muscle.ClearVelocities</c>, the game's own reset, used when its puppet teleports).
	/// </summary>
	public static void ForgetMotion(PlayerMovement pm)
	{
		Il2CppReferenceArray<Muscle> muscles = Muscles(pm);
		if (ReferenceEquals(muscles, null))
		{
			return;
		}
		Vector3 zero = default;
		for (int i = 0; i < muscles.Length; i++)
		{
			Muscle muscle = muscles[i];
			if (ReferenceEquals(muscle, null))
			{
				continue;
			}
			Rigidbody body = muscle._rigidbody_k__BackingField;
			if (!ReferenceEquals(body, null))
			{
				body.velocity = zero;
				body.angularVelocity = zero;
			}
			if (!ReferenceEquals(muscle.target, null))
			{
				muscle.ClearVelocities();
			}
			else
			{
				muscle.mappedVelocity = zero;
				muscle.mappedAngularVelocity = zero;
			}
		}
	}

	/// <summary>
	/// Something solid under a saved foot spot (the foot's target, about 10 cm above what he stands
	/// on): a ray down from 30 cm above it to 45 cm below, or failing that anything round the bottom
	/// of the foot (the ray can start inside what it should find). The ground the way the game's
	/// teleport looks for it, items included (he may have stood on one).
	/// </summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static bool GroundUnder(Vector3 spot)
	{
		if (_mask == 0)
		{
			_mask = LayerCache.PropTerrainMask.m_Mask;
			if (_mask == 0)
			{
				// The game's layer numbers (ConstGenConstants._LAYERS): Default, Terrain, Props, PropsNoCamera.
				_mask = 1 | (1 << 8) | (1 << 16) | (1 << 23);
			}
		}
		Vector3 down = Fast.V3(0f, -1f, 0f);
		if (Physics.Raycast(Fast.V3(spot.x, spot.y + 0.3f, spot.z), down, 0.75f, _mask, QueryTriggerInteraction.Ignore))
		{
			return true;
		}
		return Physics.CheckSphere(Fast.V3(spot.x, spot.y - 0.12f, spot.z), 0.12f, _mask, QueryTriggerInteraction.Ignore);
	}

	/// <summary>Where his pelvis (the ragdoll's first part) is, and how fast it's going.</summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static bool Pelvis(out Vector3 position, out float speed)
	{
		Il2CppReferenceArray<Muscle> muscles = Muscles(PlayerMovement.me);
		Muscle first = (ReferenceEquals(muscles, null) || muscles.Length == 0) ? null : muscles[0];
		Rigidbody body = ReferenceEquals(first, null) ? null : first._rigidbody_k__BackingField;
		if (ReferenceEquals(body, null))
		{
			position = default;
			speed = 0f;
			return false;
		}
		position = body.position;
		Vector3 v = body.velocity;
		speed = Fast.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
		return true;
	}

	/// <summary>Nate is down after a fall (<c>PlayerMovement.dead</c>).</summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static bool Down()
	{
		PlayerMovement pm = PlayerMovement.me;
		return !ReferenceEquals(pm, null) && pm.dead;
	}

	/// <summary>
	/// How his ragdoll and the game's record of his motion stand, for the log: "puppet Active
	/// (asked Active) Alive, stored speeds up to 0.0 m/s, hips going 0.1 m/s, torso 0.1 m/s, both
	/// feet off the ground 0.00 s, up".
	/// </summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void Describe(System.Text.StringBuilder text)
	{
		PlayerMovement pm = PlayerMovement.me;
		if (ReferenceEquals(pm, null))
		{
			text.Append("Nate isn't there");
			return;
		}
		PuppetMaster puppet = pm.pm;
		if (!ReferenceEquals(puppet, null))
		{
			text.Append("puppet ").Append(puppet.activeMode.ToString());
			if (puppet.mode != puppet.activeMode)
			{
				text.Append(" (asked ").Append(puppet.mode.ToString()).Append(')');
			}
			if (puppet._isSwitchingMode_k__BackingField)
			{
				text.Append(" switching");
			}
			text.Append(' ').Append(puppet.activeState.ToString());
			if (puppet.state != puppet.activeState)
			{
				text.Append(" (asked ").Append(puppet.state.ToString()).Append(')');
			}
			text.Append(", stored speeds up to ").Append(StoredSpeed(Muscles(pm)).ToString("0.0")).Append(" m/s, ");
		}
		text.Append("hips going ").Append(Length(pm.hipVel).ToString("0.0")).Append(" m/s, torso ").Append(Length(pm.torsoVel).ToString("0.0"))
			.Append(" m/s, both feet off the ground ").Append(pm.timeWithBothFeetInAir.ToString("0.00")).Append(" s, ")
			.Append(pm.dead ? "DOWN" : "up");
	}

	/// <summary>The fastest speed the puppet keeps for any part, to hand back on switching on or going limp.</summary>
	private static float StoredSpeed(Il2CppReferenceArray<Muscle> muscles)
	{
		float fastest = 0f;
		for (int i = 0; !ReferenceEquals(muscles, null) && i < muscles.Length; i++)
		{
			Muscle muscle = muscles[i];
			if (!ReferenceEquals(muscle, null))
			{
				float speed = Length(muscle.mappedVelocity);
				if (speed > fastest)
				{
					fastest = speed;
				}
			}
		}
		return fastest;
	}

	private static float Length(Vector3 v)
	{
		return Fast.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
	}

	private static Il2CppReferenceArray<Muscle> Muscles(PlayerMovement pm)
	{
		if (ReferenceEquals(pm, null))
		{
			return null;
		}
		PuppetMaster puppet = pm.pm;
		return ReferenceEquals(puppet, null) ? null : puppet.muscles;
	}
}
