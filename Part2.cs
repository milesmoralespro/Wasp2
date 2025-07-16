
        public OverlayForm()
        {
            InitializeComponent();
            SetupOverlay();
            InitializeOCR();
            RegisterGlobalHotkey();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form properties
            this.AutoScaleDimensions = new SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(800, 600);
            this.Name = "OverlayForm";
            this.Text = "";

            this.ResumeLayout(false);
        }

        private void InitializeOCR()
        {
            try
            {
                // Initialize Tesseract engine
                // Make sure tessdata folder is in your application's output directory
                string tessDataPath = Path.Combine(Application.StartupPath, "tessdata");

                if (!Directory.Exists(tessDataPath))
                {
                    MessageBox.Show($"Tessdata folder not found at: {tessDataPath}\nPlease ensure tessdata folder with eng.traineddata is in your application directory.",
                        "OCR Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                ocrEngine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);

                // Optional: Configure OCR settings for better accuracy
                ocrEngine.DefaultPageSegMode = PageSegMode.Auto;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize OCR engine: {ex.Message}",
                    "OCR Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string ExtractTextFromImage(Bitmap image)
        {
            if (ocrEngine == null) return "";

            try
            {
                using (var img = PixConverter.ToPix(image))
                {
                    using (var page = ocrEngine.Process(img))
                    {
                        return page.GetText();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OCR Error: {ex.Message}");
                return "";
            }
        }

        private void SetupOverlay()
        {
            // Make the form a fixed size window instead of fullscreen
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Normal;
            this.Size = new Size(800, 700); // Updated size
            this.Location = currentPosition; // Set initial position
            this.TopMost = true;
            this.ShowInTaskbar = false; // Hide from taskbar
            this.ShowIcon = false;

            // Set transparent background
            this.BackColor = Color.Black;
            this.TransparencyKey = Color.Magenta; // Use magenta as transparent key
            this.Opacity = 1.0; // Full opacity, transparency handled by layered window

            // Create semi-transparent background panel
            Panel backgroundPanel = new Panel();
            backgroundPanel.BackColor = Color.Black;
            backgroundPanel.Size = this.Size;
            backgroundPanel.Location = new Point(0, 0);
            backgroundPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.Controls.Add(backgroundPanel);

            // Create screenshot panel for thumbnails (fixed position at top)
            screenshotPanel = new FlowLayoutPanel();
            screenshotPanel.FlowDirection = FlowDirection.LeftToRight;
            screenshotPanel.AutoSize = true;
            screenshotPanel.MaximumSize = new Size(this.Width - 20, 100);
            screenshotPanel.Location = new Point(10, 10);
            screenshotPanel.BackColor = Color.Transparent;
            screenshotPanel.WrapContents = true;
            screenshotPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            backgroundPanel.Controls.Add(screenshotPanel);

            // Create display labels for language and type (fixed position)
            CreateDisplayLabels(backgroundPanel);

            // Create scrollable Gemini response area
            Panel responseContainer = new Panel();
            responseContainer.Location = new Point(10, 120); // Below screenshots and labels
            responseContainer.Size = new Size(this.Width - 20, this.Height - 130);
            responseContainer.BackColor = Color.Transparent;
            responseContainer.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            backgroundPanel.Controls.Add(responseContainer);

            geminiResponseLabel = new RichTextBox();
            FormatGeminiResponse("No analysis generated yet. Take screenshots and press Ctrl+Space to analyze.");
            geminiResponseLabel.Font = new Font("Segoe UI", 9, FontStyle.Regular);
            geminiResponseLabel.ForeColor = Color.LightGray;
            geminiResponseLabel.BackColor = Color.Black; // Dark background
            geminiResponseLabel.Size = new Size(responseContainer.Width - 20, responseContainer.Height - 20);
            geminiResponseLabel.Location = new Point(10, 10);
            geminiResponseLabel.ReadOnly = true;
            geminiResponseLabel.ScrollBars = RichTextBoxScrollBars.Vertical;
            geminiResponseLabel.BorderStyle = BorderStyle.None;
            geminiResponseLabel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            // Create scrollable panel for response
            Panel scrollablePanel = new Panel();
            scrollablePanel.Location = new Point(0, 0);
            scrollablePanel.Size = responseContainer.Size;
            scrollablePanel.BackColor = Color.Transparent;
            scrollablePanel.AutoScroll = true;
            scrollablePanel.Controls.Add(geminiResponseLabel);
            responseContainer.Controls.Add(scrollablePanel);

            // Add only screenshots to interactive controls
            interactiveControls.Add(screenshotPanel);
            interactiveControls.Add(scrollablePanel); // Make response area interactive too

            // Start hidden
            this.Visible = false;

            // Setup advanced window properties
            this.Load += OverlayForm_Load;
        }

        private void CreateDisplayLabels(Panel parentPanel)
        {
            const int labelWidth = 200;
            const int labelHeight = 25;
            const int margin = 10;
            const int spacing = 5;

            // Create Language display label
            languageDisplayLabel = new Label();
            languageDisplayLabel.Size = new Size(labelWidth, labelHeight);
            languageDisplayLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            languageDisplayLabel.BackColor = Color.Transparent;
            languageDisplayLabel.TextAlign = ContentAlignment.MiddleLeft;

            // Position in top right corner
            languageDisplayLabel.Location = new Point(
                parentPanel.Width - labelWidth - margin,
                margin
            );

            // Create Type display label
            typeDisplayLabel = new Label();
            typeDisplayLabel.Size = new Size(labelWidth, labelHeight);
            typeDisplayLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            typeDisplayLabel.BackColor = Color.Transparent;
            typeDisplayLabel.TextAlign = ContentAlignment.MiddleLeft;

            // Position below language label
            typeDisplayLabel.Location = new Point(
                parentPanel.Width - labelWidth - margin,
                margin + labelHeight + spacing
            );

            // Create AI Type display label
            aiTypeDisplayLabel = new Label();
            aiTypeDisplayLabel.Size = new Size(labelWidth, labelHeight);
            aiTypeDisplayLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            aiTypeDisplayLabel.BackColor = Color.Transparent;
            aiTypeDisplayLabel.TextAlign = ContentAlignment.MiddleLeft;

            // Position below type label
            aiTypeDisplayLabel.Location = new Point(
                parentPanel.Width - labelWidth - margin,
                margin + (labelHeight + spacing) * 2
            );

            // Add AI type label to parent panel
            parentPanel.Controls.Add(aiTypeDisplayLabel);

            // Add labels to parent panel
            parentPanel.Controls.Add(languageDisplayLabel);
            parentPanel.Controls.Add(typeDisplayLabel);

            // Update display text
            UpdateDisplayText();
        }

        private void UpdateDisplayText()
        {
            string currentLanguage = languageOptions[selectedLanguageIndex];
            string currentType = typeOptions[selectedTypeIndex];
            string currentAiType = aiTypeOptions[selectedAiTypeIndex];

            if (selectedOptionType == 0)
            {
                languageDisplayLabel.Text = $"Language: {currentLanguage}";
                languageDisplayLabel.ForeColor = Color.Yellow;
                typeDisplayLabel.Text = $"Type: {currentType}";
                typeDisplayLabel.ForeColor = Color.White;
                aiTypeDisplayLabel.Text = $"AI: {currentAiType}";
                aiTypeDisplayLabel.ForeColor = Color.White;
            }
            else if (selectedOptionType == 1) // type selected
            {
                languageDisplayLabel.Text = $"Language: {currentLanguage}";
                languageDisplayLabel.ForeColor = Color.White;
                typeDisplayLabel.Text = $"Type: {currentType}";
                typeDisplayLabel.ForeColor = Color.Yellow;
                aiTypeDisplayLabel.Text = $"AI: {currentAiType}";
                aiTypeDisplayLabel.ForeColor = Color.White;
            }
            else if(selectedOptionType == 2) // AI type selected
            {
                languageDisplayLabel.Text = $"Language: {currentLanguage}";
                languageDisplayLabel.ForeColor = Color.White;
                typeDisplayLabel.Text = $"Type: {currentType}";
                typeDisplayLabel.ForeColor = Color.White;
                aiTypeDisplayLabel.Text = $"AI: {currentAiType}";
                aiTypeDisplayLabel.ForeColor = Color.Yellow;
            }
        }

        private void ToggleSelection()
        {
            selectedOptionType = (selectedOptionType + 1) % 3; // Cycle through 0, 1, 2
            UpdateDisplayText();
        }

        private void CycleSelectedValue()
        {
            if (selectedOptionType == 0) // language
            {
                selectedLanguageIndex = (selectedLanguageIndex + 1) % languageOptions.Length;
            }
            else if (selectedOptionType == 1) // type
            {
                selectedTypeIndex = (selectedTypeIndex + 1) % typeOptions.Length;
            }
            else // ai_type
            {
                selectedAiTypeIndex = (selectedAiTypeIndex + 1) % aiTypeOptions.Length;
            }
            UpdateDisplayText();
        }

        private void OverlayForm_Load(object sender, EventArgs e)
        {
            SetupAdvancedWindowProperties();
        }

        private void SetupAdvancedWindowProperties()
        {
            // Get current extended window style
            int exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);

            // Add layered and tool window flags (NO WS_EX_TRANSPARENT here - we'll handle it manually)
            exStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;

            // Set the new extended style
            SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle);

            // Set layered window attributes for semi-transparency
            SetLayeredWindowAttributes(this.Handle, 0, 180, LWA_ALPHA); // 200/255 = ~78% opacity

            // CRITICAL: Exclude from screen capture (Windows 10+)
            SetWindowDisplayAffinity(this.Handle, WDA_EXCLUDEFROMCAPTURE);

            // Additional DWM attributes for better exclusion (Windows 11)
            try
            {
                uint excluded = 1;
                DwmSetWindowAttribute(this.Handle, DWMWA_EXCLUDED_FROM_PEEK, ref excluded, sizeof(uint));
            }
            catch
            {
                // Ignore if DWM attributes are not supported
            }
        }

        // Override WndProc to handle hit testing for selective click-through
        protected override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;

            if (m.Msg == WM_HOTKEY)
            {
                int hotkeyId = m.WParam.ToInt32();

                switch (hotkeyId)
                {
                    case HOTKEY_ID:
                        ToggleOverlay();
                        break;
                    case HOTKEY_ID_UP:
                        MoveOverlay(0, -moveStep);
                        break;
                    case HOTKEY_ID_DOWN:
                        MoveOverlay(0, moveStep);
                        break;
                    case HOTKEY_ID_LEFT:
                        MoveOverlay(-moveStep, 0);
                        break;
                    case HOTKEY_ID_RIGHT:
                        MoveOverlay(moveStep, 0);
                        break;
                    case HOTKEY_ID_SCREENSHOT:
                        TakeScreenshot();
                        break;
                    case HOTKEY_ID_CLEAR:
                        ClearScreenshots();
                        break;
                    case HOTKEY_ID_SEND_REQUEST:
                        SendGeminiRequestManually();
                        break;
                    case HOTKEY_ID_TOGGLE_SELECTION:
                        ToggleSelection();
                        break;
                    case HOTKEY_ID_CYCLE_VALUES:
                        CycleSelectedValue();
                        break;
                    case HOTKEY_ID_VIEW_TEXT:
                        ShowExtractedText();
                        break;
                }
            }
            else if (m.Msg == WM_NCHITTEST)
            {
                // Handle hit testing for selective click-through
                Point screenPoint = new Point(m.LParam.ToInt32());
                Point clientPoint = this.PointToClient(screenPoint);

                // Check if mouse is over any interactive control
                bool overInteractiveControl = false;
                foreach (Control control in interactiveControls)
                {
                    if (control.Visible && IsPointInControl(clientPoint, control))
                    {
                        overInteractiveControl = true;
                        break;
                    }
                }

                // If over interactive control, allow normal hit testing
                if (overInteractiveControl)
                {
                    m.Result = (IntPtr)HTCLIENT;
                    return;
                }

                // Otherwise, make it click-through
                m.Result = (IntPtr)HTTRANSPARENT;
                return;
            }

            base.WndProc(ref m);
        }

        private bool IsPointInControl(Point point, Control control)
        {
            // Convert control bounds to form coordinates
            Rectangle controlBounds = GetControlBounds(control);

            // Add some padding for easier interaction
            controlBounds.Inflate(5, 5);

            return controlBounds.Contains(point);
        }

        private Rectangle GetControlBounds(Control control)
        {
            // Get the absolute bounds of the control within the form
            Rectangle bounds = control.Bounds;
            Control parent = control.Parent;

            while (parent != null && parent != this)
            {
                bounds.Offset(parent.Location);
                parent = parent.Parent;
            }

            return bounds;
        }

        private void RegisterGlobalHotkey()
        {
            // Register Ctrl+B as global hotkey for toggle
            if (!RegisterHotKey(this.Handle, HOTKEY_ID, MOD_CONTROL, VK_B))
            {
                MessageBox.Show("Failed to register hotkey Ctrl+B. Another application might be using it.",
                    "Hotkey Registration Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // Register movement hotkeys (Ctrl + Alt + Arrow keys)
            RegisterHotKey(this.Handle, HOTKEY_ID_UP, MOD_CONTROL | MOD_ALT, VK_UP);
            RegisterHotKey(this.Handle, HOTKEY_ID_DOWN, MOD_CONTROL | MOD_ALT, VK_DOWN);
            RegisterHotKey(this.Handle, HOTKEY_ID_LEFT, MOD_CONTROL | MOD_ALT, VK_LEFT);
            RegisterHotKey(this.Handle, HOTKEY_ID_RIGHT, MOD_CONTROL | MOD_ALT, VK_RIGHT);

            // Register screenshot hotkeys
            RegisterHotKey(this.Handle, HOTKEY_ID_SCREENSHOT, MOD_CONTROL, VK_H);
            RegisterHotKey(this.Handle, HOTKEY_ID_CLEAR, MOD_CONTROL | MOD_ALT, VK_D);
            RegisterHotKey(this.Handle, HOTKEY_ID_SEND_REQUEST, MOD_CONTROL, VK_SPACE);

            // Register selection hotkeys
            RegisterHotKey(this.Handle, HOTKEY_ID_TOGGLE_SELECTION, MOD_CONTROL | MOD_SHIFT, VK_S);
            RegisterHotKey(this.Handle, HOTKEY_ID_CYCLE_VALUES, MOD_CONTROL | MOD_SHIFT, VK_UP);

            // Register text viewing hotkey
            RegisterHotKey(this.Handle, HOTKEY_ID_VIEW_TEXT, MOD_CONTROL | MOD_SHIFT, VK_T);
        }

        public void ToggleOverlay()
        {
            isVisible = !isVisible;

            if (isVisible)
            {
                this.Visible = true;
                // Bring to front and make topmost
                this.BringToFront();
                SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

                // Reapply screen capture exclusion after showing
                SetWindowDisplayAffinity(this.Handle, WDA_EXCLUDEFROMCAPTURE);
            }
            else
            {
                this.Visible = false;
            }
        }
