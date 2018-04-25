using System;
using System.Data;
using System.Windows.Forms;

namespace SharpSim
{
	public class DataSetProcessor
	{
		private static OpenFileDialog ofd1 = new OpenFileDialog();

		private static SaveFileDialog sfd1 = new SaveFileDialog();

		public static DataSet RunRead()
		{
			DataSet dataSet = new DataSet();
			DataSetProcessor.ofd1.Title = "Select Simulation File";
			DataSetProcessor.ofd1.FileName = "*.xml";
			DataSetProcessor.ofd1.InitialDirectory = Application.StartupPath;
			if (DataSetProcessor.ofd1.ShowDialog() == DialogResult.OK)
			{
				string fileName = DataSetProcessor.ofd1.FileName;
				dataSet.ReadXml(fileName, XmlReadMode.Auto);
			}
			return dataSet;
		}

		public static void WriteXml(DataSet W_Ds)
		{
			DataSetProcessor.sfd1.Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*";
			DataSetProcessor.sfd1.InitialDirectory = Application.StartupPath;
			if (DataSetProcessor.sfd1.ShowDialog() == DialogResult.OK)
			{
				string fileName = DataSetProcessor.sfd1.FileName;
				W_Ds.WriteXml(fileName, XmlWriteMode.IgnoreSchema);
			}
		}
	}
}
