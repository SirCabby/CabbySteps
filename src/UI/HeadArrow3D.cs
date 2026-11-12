using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;

namespace CabbySteps;

/// <summary>
/// Arrow style 2: a green 3D arrow mesh floating above Nate's head, pointing
/// (in the horizontal plane) at the tracked target.
/// </summary>
public class HeadArrow3D
{
	private GameObject _root;

	private Transform _tr;

	private bool _failed;

	/// <summary>The arrow exists and is switched on, as last set: asking the game would cost a call every frame.</summary>
	private bool _built;

	private bool _shown;

	private static readonly Vector3 Up = Fast.V3(0f, 1f, 0f);

	private readonly MelonLogger.Instance _log;

	public HeadArrow3D(MelonLogger.Instance log)
	{
		_log = log;
	}

	public bool Failed => _failed;

	/// <summary>Called every frame: show/hide the arrow and keep it above Nate, aimed at the target.</summary>
	public void Sync(bool active, PlayerTracker player, Poi target, float height)
	{
		if (_failed)
		{
			return;
		}
		if (!active || target == null || player == null || !player.Found)
		{
			if (_shown)
			{
				_shown = false;
				try
				{
					_root.SetActive(value: false);
				}
				catch
				{
					_built = false;
				}
			}
			return;
		}
		if (!_built)
		{
			if (!TryBuild())
			{
				_failed = true;
				return;
			}
			_built = true;
			_shown = false;
		}
		try
		{
			Vector3 toTarget = WorldLoop.Delta(player.WorldPos, target.World);
			float bob = Fast.Sin(Frame.Now * 2.5f) * 0.03f;
			_tr.position = Fast.V3(player.WorldPos.x, player.WorldPos.y + height + bob, player.WorldPos.z);
			float flatSq = toTarget.x * toTarget.x + toTarget.z * toTarget.z;
			if (flatSq > 0.0004f)
			{
				float inv = 1f / Fast.Sqrt(flatSq);
				_tr.rotation = Quaternion.LookRotation(Fast.V3(toTarget.x * inv, 0f, toTarget.z * inv), Up);
			}
			if (!_shown)
			{
				_root.SetActive(value: true);
				_shown = true;
			}
		}
		catch
		{
			// The arrow was destroyed under us: make it again next frame.
			_built = false;
			_shown = false;
		}
	}

	private bool TryBuild()
	{
		try
		{
			_root = new GameObject("BSM_HeadArrow3D");
			UnityEngine.Object.DontDestroyOnLoad(_root);
			_root.hideFlags = HideFlags.HideAndDontSave;
			_tr = _root.transform;
			_tr.localScale = Vector3.one * 0.5f;
			_root.AddComponent<MeshFilter>().mesh = BuildArrowMesh();
			MeshRenderer renderer = _root.AddComponent<MeshRenderer>();
			renderer.shadowCastingMode = ShadowCastingMode.Off;
			renderer.receiveShadows = false;
			Material material = MakeMaterial();
			if (material != null)
			{
				renderer.material = material;
			}
			_root.SetActive(value: false);
			_log?.Msg("[HeadArrow3D] built world arrow mesh");
			return true;
		}
		catch (Exception ex)
		{
			_log?.Warning("[HeadArrow3D] build failed, disabling 3D arrow: " + ex.Message);
			return false;
		}
	}

	/// <summary>A flat 7-point arrow outline (pointing +Z) extruded 0.18 thick, double-sided.</summary>
	private Mesh BuildArrowMesh()
	{
		float[,] outline = new float[7, 2]
		{
			{ -0.12f, -0.5f },
			{ 0.12f, -0.5f },
			{ 0.12f, 0.15f },
			{ 0.3f, 0.15f },
			{ 0f, 0.6f },
			{ -0.3f, 0.15f },
			{ -0.12f, 0.15f }
		};
		int n = 7;
		List<Vector3> verts = new List<Vector3>();
		for (int i = 0; i < n; i++)
		{
			verts.Add(new Vector3(outline[i, 0], 0.09f, outline[i, 1]));
		}
		for (int i = 0; i < n; i++)
		{
			verts.Add(new Vector3(outline[i, 0], -0.09f, outline[i, 1]));
		}
		List<int> tris = new List<int>();
		// Top and bottom caps: shaft (two triangles) + head (one).
		tris.AddRange(new int[9] { 0, 1, 2, 0, 2, 6, 3, 4, 5 });
		tris.AddRange(new int[9] { 7, 8, 9, 7, 9, 13, 10, 11, 12 });
		// Side walls.
		for (int i = 0; i < n; i++)
		{
			int next = (i + 1) % n;
			tris.AddRange(new int[6]
			{
				i,
				next,
				next + n,
				i,
				next + n,
				i + n
			});
		}
		// Every triangle again with reversed winding, so no face is ever culled.
		List<int> both = new List<int>(tris.Count * 2);
		both.AddRange(tris);
		for (int i = 0; i < tris.Count; i += 3)
		{
			both.Add(tris[i]);
			both.Add(tris[i + 2]);
			both.Add(tris[i + 1]);
		}
		Il2CppStructArray<Vector3> vertArray = new Il2CppStructArray<Vector3>(verts.Count);
		Il2CppStructArray<Color> colorArray = new Il2CppStructArray<Color>(verts.Count);
		Il2CppStructArray<Vector2> uvArray = new Il2CppStructArray<Vector2>(verts.Count);
		Color green = new Color(0.3f, 1f, 0.25f, 1f);
		for (int i = 0; i < verts.Count; i++)
		{
			vertArray[i] = verts[i];
			colorArray[i] = green;
			uvArray[i] = new Vector2(0.5f, 0.5f);
		}
		Il2CppStructArray<int> triArray = new Il2CppStructArray<int>(both.Count);
		for (int i = 0; i < both.Count; i++)
		{
			triArray[i] = both[i];
		}
		Mesh mesh = new Mesh();
		mesh.vertices = vertArray;
		mesh.colors = colorArray;
		mesh.uv = uvArray;
		mesh.triangles = triArray;
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();
		return mesh;
	}

	/// <summary>An unlit green material from the first shader the game actually has.</summary>
	private Material MakeMaterial()
	{
		Color color = new Color(0.3f, 1f, 0.25f, 1f);
		Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
		if (shader == null)
		{
			shader = Shader.Find("Unlit/Color");
		}
		if (shader == null)
		{
			shader = Shader.Find("Unlit/Texture");
		}
		if (shader == null)
		{
			shader = Shader.Find("Sprites/Default");
		}
		if (shader == null)
		{
			shader = Shader.Find("Universal Render Pipeline/Lit");
		}
		if (shader == null)
		{
			shader = Shader.Find("Standard");
		}
		Material material = ((shader != null) ? new Material(shader) : null);
		_log?.Msg("[HeadArrow3D] shader=" + ((shader != null) ? shader.name : "<none>"));
		if (material == null)
		{
			return null;
		}
		try
		{
			material.color = color;
		}
		catch
		{
		}
		TrySetColor(material, "_Color", color);
		TrySetColor(material, "_BaseColor", color);
		TrySetColor(material, "_TintColor", color);
		TrySetColor(material, "_EmissionColor", color);
		TrySetInt(material, "_Cull", 0);
		return material;
	}

	private void TrySetColor(Material m, string prop, Color c)
	{
		try
		{
			if (m.HasProperty(prop))
			{
				m.SetColor(prop, c);
			}
		}
		catch
		{
		}
	}

	private void TrySetInt(Material m, string prop, int v)
	{
		try
		{
			if (m.HasProperty(prop))
			{
				m.SetInt(prop, v);
			}
		}
		catch
		{
		}
	}
}
