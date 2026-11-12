using System;
using System.Runtime.CompilerServices;
using Il2Cpp;
using Il2CppCinemachine;
using MelonLoader;
using UnityEngine;

namespace CabbySteps;

/// <summary>
/// Game hooks: the first-person toggle, and whether the game's own menu is open.
/// </summary>
public static class GameBridge
{
	private static MelonLogger.Instance _log;

	private static PlayerMovement _pmCache;

	private static Hat _fakeHat;

	private static Hat _prevHat;

	/// <summary>Whether the user wants first person on (the game keeps trying to leave it).</summary>
	private static bool _want;

	private static bool _menuWarned;

	/// <summary>How often to ask whether the game's menu is open: each look is several calls into the game.</summary>
	private const float MenuCheckInterval = 0.1f;

	private static float _menuCheckedAt = float.MinValue;

	private static bool _menuOpen;

	// First person's camera, kept between frames for the PlayerMovement it belongs to.
	private static IntPtr _soloFor;

	private static CinemachineVirtualCamera _soloCam;

	private static GameObject _soloObject;

	private static ICinemachineCamera _soloInterface;

	/// <summary>
	/// The game's own menu is up (pause or title, Menu.paused), or it's loading or restarting the
	/// scene. The map stays out of the way then: the menu's buttons sit behind it.
	/// </summary>
	public static bool GameMenuOpen()
	{
		float now = Frame.Now;
		if (now - _menuCheckedAt < MenuCheckInterval)
		{
			return _menuOpen;
		}
		_menuCheckedAt = now;
		try
		{
			_menuOpen = MenuOpenUnchecked();
		}
		catch (Exception ex)
		{
			if (!_menuWarned)
			{
				_menuWarned = true;
				_log?.Warning("Can't tell whether the game's menu is open: " + ex.Message);
			}
			_menuOpen = false;
		}
		return _menuOpen;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool MenuOpenUnchecked()
	{
		Menu menu = Menu.me;
		return menu != null && (menu.paused || menu.loading || menu.restartingScene);
	}

	public static void Init(MelonLogger.Instance log)
	{
		_log = log;
	}

	private static PlayerMovement Player()
	{
		try
		{
			return UnityEngine.Object.FindObjectOfType<PlayerMovement>();
		}
		catch (Exception ex)
		{
			_log?.Warning("FindObjectOfType<PlayerMovement> failed: " + ex.Message);
			return null;
		}
	}

	public static string ToggleFirstPerson(out bool nowFirstPerson)
	{
		PlayerMovement pm = Player();
		_pmCache = pm;
		if (pm == null)
		{
			nowFirstPerson = false;
			return "First person: player not found yet";
		}
		_want = !_want;
		try
		{
			if (_want)
			{
				EnterFP(pm);
			}
			else
			{
				ExitFP(pm);
			}
		}
		catch (Exception ex)
		{
			_log?.Error("[fpv] toggle failed: " + ex);
		}
		bool inFp = false;
		try
		{
			inFp = pm.inFirstPerson;
		}
		catch
		{
		}
		LogDiag(pm, _want ? "on" : "off");
		nowFirstPerson = _want && inFp;
		return "First person: " + (_want ? "ON" : "OFF");
	}

	// The game only allows first person while Nate wears the GoPro hat, so we
	// hand it an invisible GoPro hat that never goes on his head.
	private static void EnterFP(PlayerMovement pm)
	{
		EnsureFakeHat();
		try
		{
			_prevHat = pm.currentHat;
		}
		catch
		{
			_prevHat = null;
		}
		try
		{
			if (_fakeHat != null)
			{
				pm.currentHat = _fakeHat;
			}
		}
		catch (Exception ex)
		{
			_log?.Warning("set currentHat: " + ex.Message);
		}
		try
		{
			if (!pm.inFirstPerson)
			{
				pm.ToggleFPS();
			}
		}
		catch (Exception ex2)
		{
			_log?.Warning("ToggleFPS: " + ex2.Message);
		}
		ForceSolo(pm, on: true);
	}

	private static void ExitFP(PlayerMovement pm)
	{
		ForceSolo(pm, on: false);
		try
		{
			if (pm.inFirstPerson)
			{
				pm.ToggleFPS();
			}
		}
		catch
		{
		}
		try
		{
			pm.currentHat = _prevHat;
		}
		catch
		{
		}
		_prevHat = null;
	}

	/// <summary>
	/// Leave first person for a while, if it's on: the game's teleport takes off whatever hat Nate
	/// wears and puts it back on, and the virtual GoPro is no real hat. True if it was on, in which
	/// case <see cref="ResumeFirstPerson"/> puts it back.
	/// </summary>
	public static bool SuspendFirstPerson()
	{
		if (!_want)
		{
			return false;
		}
		_want = false;
		PlayerMovement pm = _pmCache;
		if (pm == null)
		{
			pm = Player();
		}
		if (pm != null)
		{
			try
			{
				ExitFP(pm);
			}
			catch (Exception ex)
			{
				_log?.Warning("[fpv] suspend failed: " + ex.Message);
			}
			LogDiag(pm, "suspended");
		}
		return true;
	}

	/// <summary>Back into first person after <see cref="SuspendFirstPerson"/>.</summary>
	public static void ResumeFirstPerson()
	{
		if (_want)
		{
			return;
		}
		PlayerMovement pm = Player();
		_pmCache = pm;
		if (pm == null)
		{
			return;
		}
		_want = true;
		try
		{
			EnterFP(pm);
		}
		catch (Exception ex)
		{
			_log?.Error("[fpv] resume failed: " + ex);
		}
		LogDiag(pm, "resumed");
	}

	/// <summary>Called every frame: re-asserts first person if the game dropped out of it.</summary>
	public static void MaintainFirstPerson()
	{
		if (!_want)
		{
			return;
		}
		PlayerMovement pm = _pmCache;
		// Unity's own null check (is it destroyed?) is a call into the game: once a second is enough.
		if (ReferenceEquals(pm, null) || (Frame.Count % 60 == 0 && pm == null))
		{
			pm = (_pmCache = Player());
			if (pm == null)
			{
				return;
			}
		}
		try
		{
			if (!ReferenceEquals(_fakeHat, null))
			{
				try
				{
					Hat current = pm.currentHat;
					if (ReferenceEquals(current, null) || current.Pointer != _fakeHat.Pointer)
					{
						pm.currentHat = _fakeHat;
					}
				}
				catch
				{
				}
			}
			bool inFp = false;
			try
			{
				inFp = pm.inFirstPerson;
			}
			catch
			{
			}
			if (!inFp)
			{
				try
				{
					pm.ToggleFPS();
				}
				catch
				{
				}
			}
			ForceSolo(pm, on: true);
		}
		catch
		{
			_pmCache = null;
		}
	}

	private static void EnsureFakeHat()
	{
		if (_fakeHat != null)
		{
			return;
		}
		try
		{
			GameObject go = new GameObject("BSM_FPGoPro");
			go.SetActive(value: false);
			UnityEngine.Object.DontDestroyOnLoad(go);
			Hat hat = go.AddComponent<Hat>();
			try
			{
				hat.dream = HatDream.goPro;
			}
			catch (Exception ex)
			{
				_log?.Warning("set dream: " + ex.Message);
			}
			_fakeHat = hat;
			_log?.Msg("[fpv] created virtual goPro hat (invisible, off-head)");
		}
		catch (Exception ex2)
		{
			_log?.Error("EnsureFakeHat failed: " + ex2);
		}
	}

	/// <summary>Make Cinemachine render through the first-person camera regardless of priorities.</summary>
	private static void ForceSolo(PlayerMovement pm, bool on)
	{
		try
		{
			if (!on)
			{
				CinemachineBrain.SoloCamera = null;
				_soloFor = IntPtr.Zero;
				return;
			}
			// Called every frame in first person: look the camera up once per PlayerMovement.
			if (_soloFor != pm.Pointer)
			{
				_soloFor = IntPtr.Zero;
				CinemachineVirtualCamera fpsCam = pm.fpsCam;
				if (fpsCam == null)
				{
					return;
				}
				_soloCam = fpsCam;
				_soloObject = fpsCam.gameObject;
				_soloInterface = fpsCam.TryCast<ICinemachineCamera>();
				_soloFor = pm.Pointer;
			}
			if (!_soloObject.activeSelf)
			{
				_soloObject.SetActive(value: true);
			}
			_soloCam.enabled = true;
			CinemachineBrain.SoloCamera = _soloInterface;
		}
		catch (Exception ex)
		{
			_soloFor = IntPtr.Zero;
			_log?.Warning("ForceSolo: " + ex.Message);
		}
	}

	private static void LogDiag(PlayerMovement pm, string tag)
	{
		try
		{
			string hat = "?";
			try
			{
				Hat currentHat = pm.currentHat;
				hat = ((currentHat != null) ? currentHat.dream.ToString() : "none");
			}
			catch
			{
			}
			bool inFp = false;
			try
			{
				inFp = pm.inFirstPerson;
			}
			catch
			{
			}
			string mainCam = "?";
			try
			{
				mainCam = ((Camera.main != null) ? Camera.main.name : "null");
			}
			catch
			{
			}
			_log?.Msg($"[fpv] {tag} inFirstPerson={inFp} currentHat={hat} main={mainCam}");
		}
		catch
		{
		}
	}
}
