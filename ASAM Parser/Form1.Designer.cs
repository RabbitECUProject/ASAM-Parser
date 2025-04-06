namespace ASAM_Tool
{
    partial class ASAMParser
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.buttonMakeA2L = new System.Windows.Forms.Button();
            this.textBoxProgress = new System.Windows.Forms.TextBox();
            this.buttonPatchOffsets = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // buttonMakeA2L
            // 
            this.buttonMakeA2L.Location = new System.Drawing.Point(12, 11);
            this.buttonMakeA2L.Name = "buttonMakeA2L";
            this.buttonMakeA2L.Size = new System.Drawing.Size(102, 23);
            this.buttonMakeA2L.TabIndex = 4;
            this.buttonMakeA2L.Text = "Create ASAM A2L";
            this.buttonMakeA2L.UseVisualStyleBackColor = true;
            this.buttonMakeA2L.Click += new System.EventHandler(this.buttonMakeA2L_Click);
            // 
            // textBoxProgress
            // 
            this.textBoxProgress.Location = new System.Drawing.Point(12, 47);
            this.textBoxProgress.Multiline = true;
            this.textBoxProgress.Name = "textBoxProgress";
            this.textBoxProgress.Size = new System.Drawing.Size(288, 124);
            this.textBoxProgress.TabIndex = 5;
            // 
            // buttonPatchOffsets
            // 
            this.buttonPatchOffsets.Location = new System.Drawing.Point(120, 11);
            this.buttonPatchOffsets.Name = "buttonPatchOffsets";
            this.buttonPatchOffsets.Size = new System.Drawing.Size(94, 24);
            this.buttonPatchOffsets.TabIndex = 6;
            this.buttonPatchOffsets.Text = "Patch Offsets";
            this.buttonPatchOffsets.UseVisualStyleBackColor = true;
            this.buttonPatchOffsets.Click += new System.EventHandler(this.button1_Click);
            // 
            // ASAMParser
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(312, 183);
            this.Controls.Add(this.buttonPatchOffsets);
            this.Controls.Add(this.textBoxProgress);
            this.Controls.Add(this.buttonMakeA2L);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ASAMParser";
            this.Text = "MDAC ECUHost ASAM A2L Tool V1.0.0.2";
            this.Load += new System.EventHandler(this.ASAMParser_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button buttonMakeA2L;
        private System.Windows.Forms.TextBox textBoxProgress;
        private System.Windows.Forms.Button buttonPatchOffsets;
    }
}

