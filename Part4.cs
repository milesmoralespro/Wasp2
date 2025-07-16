
        private void ViewFullScreenshot(int index)
        {
            if (index >= 0 && index < screenshots.Count)
            {
                // Create a simple viewer form
                Form viewer = new Form();
                viewer.Text = $"Screenshot {index + 1}";
                viewer.Size = new Size(800, 600);
                viewer.StartPosition = FormStartPosition.CenterScreen;

                PictureBox pictureBox = new PictureBox();
                pictureBox.Dock = DockStyle.Fill;
                pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
                pictureBox.Image = screenshots[index];

                viewer.Controls.Add(pictureBox);
                viewer.Show();
            }
        }

        public void ShowExtractedText()
        {
            if (string.IsNullOrWhiteSpace(extractedTextAll))
            {
                MessageBox.Show("No text has been extracted yet. Take some screenshots first!",
                    "No Text Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Create a form to display the extracted text
            Form textViewer = new Form();
            textViewer.Text = "All Extracted Text";
            textViewer.Size = new Size(800, 600);
            textViewer.StartPosition = FormStartPosition.CenterScreen;

            TextBox textBox = new TextBox();
            textBox.Dock = DockStyle.Fill;
            textBox.Multiline = true;
            textBox.ScrollBars = ScrollBars.Both;
            textBox.ReadOnly = true;
            textBox.Text = extractedTextAll;
            textBox.Font = new Font("Consolas", 10);

            // Add a copy button
            Button copyButton = new Button();
            copyButton.Text = "Copy All Text";
            copyButton.Dock = DockStyle.Bottom;
            copyButton.Height = 30;
            copyButton.Click += (s, e) => {
                Clipboard.SetText(extractedTextAll);
                MessageBox.Show("Text copied to clipboard!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            textViewer.Controls.Add(textBox);
            textViewer.Controls.Add(copyButton);
            textViewer.Show();
        }


        public string GetAllExtractedText()
        {
            return extractedTextAll;
        }

        // Add new method to clear all extracted text
        public void ClearAllExtractedText()
        {
            extractedTextAll = "";
        }

        public void ClearScreenshots()
        {
            // Dispose of all screenshot images
            foreach (Image screenshot in screenshots)
            {
                screenshot?.Dispose();
            }

            screenshots.Clear();
            // Also clear the extracted text and update preview
            extractedTextAll = "";
            FormatGeminiResponse("No analysis generated yet. Take screenshots and press Ctrl+Space to analyze.");

            UpdateScreenshotThumbnails();
        }

        // Public methods to access the dropdown values
        public string GetSelectedLanguage()
        {
            return languageOptions[selectedLanguageIndex];
        }

        public string GetSelectedType()
        {
            return typeOptions[selectedTypeIndex];
        }

        public string GetSelectedAiType()
        {
            return aiTypeOptions[selectedAiTypeIndex];
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Instead of closing, hide the form
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Visible = false;
                isVisible = false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            // Unregister all hotkeys when disposing
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            UnregisterHotKey(this.Handle, HOTKEY_ID_UP);
            UnregisterHotKey(this.Handle, HOTKEY_ID_DOWN);
            UnregisterHotKey(this.Handle, HOTKEY_ID_LEFT);
            UnregisterHotKey(this.Handle, HOTKEY_ID_RIGHT);
            UnregisterHotKey(this.Handle, HOTKEY_ID_SCREENSHOT);
            UnregisterHotKey(this.Handle, HOTKEY_ID_CLEAR);
            UnregisterHotKey(this.Handle, HOTKEY_ID_SEND_REQUEST);
            UnregisterHotKey(this.Handle, HOTKEY_ID_TOGGLE_SELECTION);
            UnregisterHotKey(this.Handle, HOTKEY_ID_CYCLE_VALUES);
            UnregisterHotKey(this.Handle, HOTKEY_ID_VIEW_TEXT);

            // Clean up screenshots
            ClearScreenshots();

            // Clean up OCR engine
            ocrEngine?.Dispose();

            // Remove screen capture exclusion
            SetWindowDisplayAffinity(this.Handle, WDA_NONE);
            base.Dispose(disposing);
        }

        // Override CreateParams to set additional window properties
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }
    }

    // Application context to keep the app running without a visible main form
    public class CustomApplicationContext : System.Windows.Forms.ApplicationContext
    {
        private OverlayForm overlayForm;
        private NotifyIcon trayIcon;

        public CustomApplicationContext()
        {
            // Create the overlay form
            overlayForm = new OverlayForm();
        }                       

        private void Exit(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            overlayForm?.Dispose();
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                trayIcon?.Dispose();
                overlayForm?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // Main Program class
    public static class Program 
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Run the application with custom context
            Application.Run(new CustomApplicationContext());
        }
    }
}
