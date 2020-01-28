﻿// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedMember.Global

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.IO;
using VisualPinball.Unity.Extensions;
using Texture = VisualPinball.Engine.VPT.Texture;

namespace VisualPinball.Unity.Importer
{
	public class TextureImporter
	{
		private readonly Texture[] _textures;

		public TextureImporter(Texture[] textures)
		{
			_textures = textures;
		}

		public void ImportTextures(string textureFolder)
		{
			Profiler.Start("Run texture job");
			using (var job = new TextureJob(_textures, textureFolder)) {
				var handle = job.Schedule(_textures.Length, 64);
				handle.Complete();
			}
			Profiler.Stop("Run texture job");

			// set filename -> texture map for OnPreprocessTexture()
			foreach (var texture in _textures) {
				var path = texture.GetUnityFilename(textureFolder);
				TexturePostProcessor.Textures[path] = texture;
			}

			// now the assets are written to disk, explicitly import them
			Profiler.Start("AssetDatabase.ImportAsset");
			AssetDatabase.ImportAsset(textureFolder, ImportAssetOptions.ImportRecursive);
			Profiler.Stop("AssetDatabase.ImportAsset");
		}
	}

	internal struct TextureJob : IJobParallelFor, IDisposable
	{
		[ReadOnly]
		private NativeArray<IntPtr> _textures;

		[ReadOnly]
		[NativeDisableUnsafePtrRestriction]
		private readonly IntPtr _textureFolder;

		public TextureJob(IEnumerable<Texture> textures, string textureFolder)
		{
			_textures = new NativeArray<IntPtr>(textures.Select(MemHelper.ToIntPtr).ToArray(), Allocator.Persistent);
			_textureFolder = MemHelper.ToIntPtr(textureFolder);
		}

		public void Execute(int index)
		{
			// unpack pointers
			var texture = MemHelper.ToObj<Texture>(_textures[index]);
			var textureFolder =  MemHelper.ToObj<string>(_textureFolder);

			// write to disk
			var path = texture.GetUnityFilename(textureFolder);
			File.WriteAllBytes(path, texture.FileContent);
		}

		public void Dispose()
		{
			_textures.Dispose();
		}
	}

	public class TexturePostProcessor : AssetPostprocessor
	{
		public static readonly Dictionary<string, Texture> Textures = new Dictionary<string, Texture>();

		public void OnPreprocessTexture()
		{
			var importer = assetImporter as UnityEditor.TextureImporter;
			if (importer != null) {
				var texture = Textures[importer.assetPath];

				importer.textureType = texture.UsageNormalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
				importer.alphaIsTransparency = !texture.IsOpaque;
				importer.isReadable = true;
				importer.mipmapEnabled = true;
				importer.filterMode = FilterMode.Bilinear;
				//EditorUtility.CompressTexture(AssetDatabase.LoadAssetAtPath<Texture2D>(importer.assetPath), texture.HasTransparentPixels ? TextureFormat.ARGB32 : TextureFormat.RGB24, UnityEditor.TextureCompressionQuality.Best);
			}
		}
	}
}
