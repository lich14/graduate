using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace SharpSim
{
	public class SimForm : Form
	{
		private string rtxt1;

		private string[] trackListArray;

		internal BackgroundWorker tSimForm;

		public bool terminated;

		private IContainer components = null;

		private RichTextBox richTextBox1;

		public RichTextBox richTextBox2;

		private GroupBox groupBox1;

		private GroupBox groupBox2;

		public SimForm()
		{
			this.InitializeComponent();
			base.Show();
			this.tSimForm = new BackgroundWorker();
			this.trackListArray = new string[Simulation.replication];
		}

		public void Subscribe(Event event1)
		{
			event1.EventExecuted = (Event.EventExecutionHandler)Delegate.Combine(event1.EventExecuted, new Event.EventExecutionHandler(this.DelegateExecute));
		}

		public void DelegateExecute(object object1, EventInfoArgs e)
		{
			if (e.evnt.parameter == null)
			{
				object obj = this.rtxt1;
				this.rtxt1 = string.Concat(new object[]
				{
					obj,
					string.Format("{0: 0.####}", e.evnt.executionTime),
					"   ",
					e.evnt.parameter,
					"   ",
					e.evnt.name,
					"\n"
				});
				string[] array;
				string[] expr_8F = array = this.trackListArray;
				IntPtr intPtr;
				int expr_98 = (int)(intPtr = (IntPtr)(Simulation.replicationNow - 1));
				obj = array[(int)intPtr];
				expr_8F[expr_98] = string.Concat(new object[]
				{
					obj,
					string.Format("{0: 0.####}", e.evnt.executionTime),
					"   ",
					e.evnt.parameter,
					"   ",
					e.evnt.name,
					"\n"
				});
			}
			else
			{
				object obj = this.rtxt1;
				this.rtxt1 = string.Concat(new object[]
				{
					obj,
					string.Format("{0: 0.####}", e.evnt.executionTime),
					"   ",
					e.evnt.parameter.identifier,
					"   ",
					e.evnt.name,
					"\n"
				});
				string[] array;
				string[] expr_184 = array = this.trackListArray;
				IntPtr intPtr;
				int expr_18D = (int)(intPtr = (IntPtr)(Simulation.replicationNow - 1));
				obj = array[(int)intPtr];
				expr_184[expr_18D] = string.Concat(new object[]
				{
					obj,
					string.Format("{0: 0.####}", e.evnt.executionTime),
					"   ",
					e.evnt.parameter.identifier,
					"   ",
					e.evnt.name,
					"\n"
				});
			}
		}

		public void Write(object sender, DoWorkEventArgs e)
		{
			while (!this.terminated)
			{
				MethodInvoker method = delegate
				{
					this.richTextBox1.Text = this.rtxt1;
				};
				base.BeginInvoke(method);
				Thread.Sleep(500);
			}
			MethodInvoker method2 = delegate
			{
				this.richTextBox1.Text = this.rtxt1;
			};
			base.BeginInvoke(method2);
			this.tSimForm.WorkerSupportsCancellation = true;
			this.tSimForm.CancelAsync();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && this.components != null)
			{
				this.components.Dispose();
			}
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			this.richTextBox1 = new RichTextBox();
			this.richTextBox2 = new RichTextBox();
			this.groupBox1 = new GroupBox();
			this.groupBox2 = new GroupBox();
			this.groupBox1.SuspendLayout();
			this.groupBox2.SuspendLayout();
			base.SuspendLayout();
			this.richTextBox1.Location = new Point(6, 24);
			this.richTextBox1.Name = "richTextBox1";
			this.richTextBox1.Size = new Size(322, 313);
			this.richTextBox1.TabIndex = 0;
			this.richTextBox1.Text = "";
			this.richTextBox2.Location = new Point(6, 19);
			this.richTextBox2.Name = "richTextBox2";
			this.richTextBox2.ReadOnly = true;
			this.richTextBox2.Size = new Size(322, 300);
			this.richTextBox2.TabIndex = 1;
			this.richTextBox2.Text = "";
			this.groupBox1.Controls.Add(this.richTextBox1);
			this.groupBox1.Location = new Point(12, 2);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new Size(334, 343);
			this.groupBox1.TabIndex = 2;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Event List";
			this.groupBox2.Controls.Add(this.richTextBox2);
			this.groupBox2.Location = new Point(12, 351);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new Size(334, 327);
			this.groupBox2.TabIndex = 3;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "System Output";
			base.AutoScaleDimensions = new SizeF(6f, 13f);
			//base.AutoScaleMode = AutoScaleMode.Font;
			base.ClientSize = new Size(359, 682);
			base.Controls.Add(this.groupBox2);
			base.Controls.Add(this.groupBox1);
			base.Location = new Point(10, 10);
			base.Name = "SimForm";
			base.StartPosition = FormStartPosition.Manual;
			this.Text = "Simulation";
			this.groupBox1.ResumeLayout(false);
			this.groupBox2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
