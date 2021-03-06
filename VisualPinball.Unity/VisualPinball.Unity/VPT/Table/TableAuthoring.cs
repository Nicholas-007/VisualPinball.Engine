﻿// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Collection;
using VisualPinball.Engine.VPT.Decal;
using VisualPinball.Engine.VPT.DispReel;
using VisualPinball.Engine.VPT.Flasher;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Light;
using VisualPinball.Engine.VPT.LightSeq;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Primitive;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Sound;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.TextBox;
using VisualPinball.Engine.VPT.Timer;
using VisualPinball.Engine.VPT.Trigger;
using VisualPinball.Unity.VPT.Table;
using Logger = NLog.Logger;
using SurfaceData = VisualPinball.Engine.VPT.Surface.SurfaceData;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Table")]
	public class TableAuthoring : ItemAuthoring<Table, TableData>
	{
		public Table Table => Item;
		public TableSerializedTextureContainer Textures => _sidecar?.textures;
		public Patcher.Patcher Patcher { get; internal set; }

		protected override string[] Children => null;

		[HideInInspector] [SerializeField] public string physicsEngineId;
		[HideInInspector] [SerializeField] public string debugUiId;
		[HideInInspector] [SerializeField] private TableSidecar _sidecar;
		private readonly Dictionary<string, Texture2D> _unityTextures = new Dictionary<string, Texture2D>();
		// note: this cache needs to be keyed on the engine material itself so that when its recreated due to property changes the unity material
		// will cache miss and get recreated as well
		private readonly Dictionary<PbrMaterial, UnityEngine.Material> _unityMaterials = new Dictionary<PbrMaterial, UnityEngine.Material>();
		/// <summary>
		/// Keeps a list of texture names that need recreation, serialized and
		/// lazy so when undo happens they'll be considered dirty again
		/// </summary>
		[HideInInspector] [SerializeField] private List<string> _dirtyTextures = new List<string>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected void Awake()
		{
			EngineProvider<IPhysicsEngine>.Set(physicsEngineId);
			EngineProvider<IPhysicsEngine>.Get().Init(this);
			if (!string.IsNullOrEmpty(debugUiId)) {
				EngineProvider<IDebugUI>.Set(debugUiId);
			}
		}

		protected virtual void Start()
		{
			if (EngineProvider<IDebugUI>.Exists) {
				EngineProvider<IDebugUI>.Get().Init(this);
			}
		}

		protected override void OnDrawGizmos()
		{
			// do nothing, base class draws all child meshes for ease of selection, but
			// that would just be everything at this level
		}

		protected override Table GetItem()
		{
			return RecreateTable();
		}

		internal TableSidecar GetOrCreateSidecar()
		{
			if (_sidecar == null) {
				_sidecar = ScriptableObject.CreateInstance<TableSidecar>();
			}
			return _sidecar;
		}

		public void AddTexture(string name, Texture2D texture)
		{
			_unityTextures[name.ToLower()] = texture;
		}

		public void MarkTextureDirty(string name)
		{
			_dirtyTextures.Add(name.ToLower());
		}

		public Texture2D GetTexture(string name)
		{
			var lowerName = name.ToLower();
			bool forceRecreate = false;
			// check to see if the texture we're after has been flagged as dirty and thus needs to be recreated from table data
			if (_dirtyTextures.Contains(lowerName)) {
				forceRecreate = true;
				_dirtyTextures.Remove(lowerName);
			}
			// don't need to recreate it, and we have the texture in cache
			if (!forceRecreate && _unityTextures.ContainsKey(lowerName)) {
				return _unityTextures[lowerName];
			}
			// create unity texture from vpe data and put in a cache for future retrievals
			var tableTex = Table.GetTexture(lowerName);
			if (tableTex != null) {
				var unityTex = tableTex.ToUnityTexture();
				_unityTextures[lowerName] = unityTex;
				return unityTex;
			}
			return null;
		}

		public void AddMaterial(PbrMaterial vpxMat, UnityEngine.Material material)
		{
			UnityEngine.Material oldMaterial = null;
			_unityMaterials.TryGetValue(vpxMat, out oldMaterial);

			_unityMaterials[vpxMat] = material;
			if (oldMaterial != null) {
				Destroy(oldMaterial);
			}
		}

		public UnityEngine.Material GetMaterial(PbrMaterial vpxMat)
		{
			if (_unityMaterials.ContainsKey(vpxMat)) {
				return _unityMaterials[vpxMat];
			}
			return null;
		}

		public Table CreateTable()
		{
			Logger.Info("Restoring table...");
			// restore table data
			var table = new Table(data);

			// restore table info
			Logger.Info("Restoring table info...");
			foreach (var k in _sidecar.tableInfo.Keys) {
				table.TableInfo[k] = _sidecar.tableInfo[k];
			}

			// restore custom info tags
			table.CustomInfoTags = _sidecar.customInfoTags;

			// replace texture container
			table.SetTextureContainer(_sidecar.textures);

			// restore game items with no game object (yet!)
			table.ReplaceAll(_sidecar.decals.Select(d => new Decal(d)));
			Restore(_sidecar.collections, table.Collections, d => new Collection(d));
			Restore(_sidecar.dispReels, table, d => new DispReel(d));
			Restore(_sidecar.flashers, table, d => new Flasher(d));
			Restore(_sidecar.lightSeqs, table, d => new LightSeq(d));
			Restore(_sidecar.plungers, table, d => new Plunger(d));
			Restore(_sidecar.textBoxes, table, d => new TextBox(d));
			Restore(_sidecar.timers, table, d => new Timer(d));

			// restore game items
			Logger.Info("Restoring game items...");
			Restore<BumperAuthoring, Bumper, BumperData>(table);
			Restore<FlipperAuthoring, Flipper, FlipperData>(table);
			Restore<GateAuthoring, Gate, GateData>(table);
			Restore<HitTargetAuthoring, HitTarget, HitTargetData>(table);
			Restore<KickerAuthoring, Kicker, KickerData>(table);
			Restore<LightAuthoring, Engine.VPT.Light.Light, LightData>(table);
			Restore<PlungerAuthoring, Plunger, PlungerData>(table);
			Restore<PrimitiveAuthoring, Primitive, PrimitiveData>(table);
			Restore<RampAuthoring, Ramp, RampData>(table);
			Restore<RubberAuthoring, Rubber, RubberData>(table);
			Restore<SpinnerAuthoring, Spinner, SpinnerData>(table);
			Restore<SurfaceAuthoring, Engine.VPT.Surface.Surface, SurfaceData>(table);
			Restore<TriggerAuthoring, Trigger, TriggerData>(table);

			return table;
		}

		public Table RecreateTable()
		{
			var table = CreateTable();

			Restore(_sidecar.sounds, table.Sounds, d => new Sound(d));

			Logger.Info("Table restored.");
			return table;
		}

		private void Restore<TComp, TItem, TData>(Table table) where TData : ItemData where TItem : Item<TData>, IRenderable where TComp : ItemAuthoring<TItem, TData>
		{
			foreach (var component in GetComponentsInChildren<TComp>(true)) {
				table.Add(component.Item);
			}
		}

		private static void Restore<TItem, TData>(IEnumerable<TData> src, IDictionary<string, TItem> dest, Func<TData, TItem> create) where TData : ItemData where TItem : Item<TData>
		{
			foreach (var d in src) {
				dest[d.GetName()] = create(d);
			}
		}

		private static void Restore<TItem, TData>(IEnumerable<TData> src, Table table, Func<TData, TItem> create) where TData : ItemData where TItem : Item<TData>
		{
			foreach (var d in src) {
				table.Add(create(d));
			}
		}
	}
}
