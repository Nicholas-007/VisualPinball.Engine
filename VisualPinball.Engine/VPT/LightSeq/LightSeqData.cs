#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
#endregion

using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.LightSeq
{
	public class LightSeqData : ItemData
	{
		[BiffString("NAME", IsWideString = true)]
		public override string Name { get; set; }

		[BiffString("COLC", IsWideString = true)]
		public string Collection;

		[BiffVertex("VCEN", Index = 0)]
		public Vertex2D V = new Vertex2D();

		[BiffVertex("CTRX", Index = 0)]
		[BiffVertex("CTRY", Index = 1)]
		public Vertex2D Center = new Vertex2D();

		[BiffInt("UPTM")]
		public int UpdateInterval = 25;

		[BiffBool("BGLS")]
		public bool Backglass = false;

		#region BIFF

		static LightSeqData()
		{
			Init(typeof(LightSeqData), Attributes);
		}

		public LightSeqData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write(ItemType.LightSeq);
			Write(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
