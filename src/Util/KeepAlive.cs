using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;

namespace CabbySteps;

/// <summary>
/// Keeps Unity objects created by the mod alive. They're Il2Cpp objects, so
/// the managed wrapper alone doesn't stop Unity unloading them or the Il2Cpp
/// GC collecting them: hide/don't-save them, take a GC handle and keep a
/// managed reference.
/// </summary>
public static class KeepAlive
{
	public static MelonLogger.Instance Log;

	private static readonly List<UnityEngine.Object> _roots = new List<UnityEngine.Object>();

	public static IntPtr Persist(UnityEngine.Object o)
	{
		o.hideFlags = HideFlags.HideAndDontSave;
		IntPtr ptr = o.Pointer;
		IL2CPP.il2cpp_gchandle_new(ptr, pinned: false);
		_roots.Add(o);
		return ptr;
	}

	/// <summary>
	/// Like Persist, and also references the texture from a sprite on a
	/// disabled, DontDestroyOnLoad SpriteRenderer so it survives scene loads.
	/// </summary>
	public static IntPtr PersistTexture(Texture2D tex)
	{
		tex.hideFlags = HideFlags.HideAndDontSave;
		IntPtr ptr = tex.Pointer;
		IL2CPP.il2cpp_gchandle_new(ptr, pinned: false);
		_roots.Add(tex);
		GameObject holder = new GameObject("BSM_TexRoot");
		UnityEngine.Object.DontDestroyOnLoad(holder);
		holder.hideFlags = HideFlags.HideAndDontSave;
		SpriteRenderer renderer = holder.AddComponent<SpriteRenderer>();
		renderer.enabled = false;
		Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
		sprite.hideFlags = HideFlags.HideAndDontSave;
		renderer.sprite = sprite;
		_roots.Add(holder);
		_roots.Add(sprite);
		Log?.Msg($"[keepalive-tex] rooted ptr={ptr.ToInt64():X} via {holder.name}");
		return ptr;
	}
}
