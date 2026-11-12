using UnityEngine;

namespace CabbySteps;

/// <summary>Finds Nate and reports his world position and facing every frame.</summary>
public class PlayerTracker
{
	/// <summary>Nate's physics body (the PuppetMaster ragdoll root) under the "Dudest" object.</summary>
	private const string BodyPath = "PuppetMaster/root.x";

	private GameObject _dudest;

	private Transform _body;

	private int _retryCooldown;

	/// <summary>The camera, looked up twice a second rather than every frame.</summary>
	private Transform _camera;

	private float _nextCameraLookup;

	public bool Found { get; private set; }

	public Vector3 WorldPos { get; private set; }

	/// <summary>Horizontal facing (from the camera when there is one), normalised.</summary>
	public Vector3 Forward { get; private set; }

	/// <summary>Compass heading of Forward in degrees (0 = +Z).</summary>
	public float HeadingDeg { get; private set; }

	public void Update()
	{
		// Plain reference checks: Unity's own == would be a call into the game every frame. A
		// body destroyed with its scene throws when used, below, and is looked for again.
		if (ReferenceEquals(_body, null))
		{
			// Searching is expensive: try again only every 30 frames.
			if (_retryCooldown-- > 0)
			{
				Found = false;
				return;
			}
			_retryCooldown = 30;
			if (_dudest == null)
			{
				_dudest = GameObject.Find("Dudest");
			}
			if (_dudest != null)
			{
				_body = _dudest.transform.Find(BodyPath);
			}
			if (_body == null)
			{
				Found = false;
				return;
			}
		}
		Vector3 forward;
		try
		{
			WorldPos = _body.position;
			float now = Frame.Now;
			if (now >= _nextCameraLookup)
			{
				_nextCameraLookup = now + 0.5f;
				Camera main = Camera.main;
				_camera = (main != null) ? main.transform : null;
			}
			forward = ReferenceEquals(_camera, null) ? _body.forward : _camera.forward;
		}
		catch
		{
			// Nate (or the camera) went with a scene change: look for them again.
			_body = null;
			_dudest = null;
			_camera = null;
			_nextCameraLookup = 0f;
			Found = false;
			return;
		}
		float flatSq = forward.x * forward.x + forward.z * forward.z;
		Forward = (flatSq < 0.0001f) ? Fast.V3(0f, 0f, 1f) : Fast.V3(forward.x / Fast.Sqrt(flatSq), 0f, forward.z / Fast.Sqrt(flatSq));
		HeadingDeg = Fast.Atan2(Forward.x, Forward.z) * Fast.Rad2Deg;
		Found = true;
	}
}
