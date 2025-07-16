
        private void MoveOverlay(int deltaX, int deltaY)
        {
            // Only move if the overlay is visible
            if (!isVisible) return;

            // Calculate new position (remove screen bounds limitation)
            Point newLocation = new Point(this.Location.X + deltaX, this.Location.Y + deltaY);

            // Update position
            this.Location = newLocation;
            currentPosition = newLocation; // Remember position for next toggle
        }

        public void TakeScreenshot()
        {
            try
            {
                // Get the currently active window (not our overlay)
                IntPtr foregroundWindow = GetForegroundWindow();

                // If the foreground window is our overlay, get the window behind it
                if (foregroundWindow == this.Handle)
                {
                    // Hide our overlay temporarily to get the window behind
                    bool wasVisible = this.Visible;
                    if (wasVisible) this.Visible = false;

                    System.Threading.Thread.Sleep(50); // Small delay to let window manager update
                    foregroundWindow = GetForegroundWindow();

                    if (wasVisible) this.Visible = true;
                }

                // Get window rectangle
                RECT windowRect;
                if (!GetWindowRect(foregroundWindow, out windowRect))
                {
                    // Fallback to full screen capture
                    CaptureFullScreen();
                    return;
                }

                // Calculate window dimensions
                int width = windowRect.Right - windowRect.Left;
                int height = windowRect.Bottom - windowRect.Top;

                if (width <= 0 || height <= 0)
                {
                    CaptureFullScreen();
                    return;
                }

                // Create bitmap for the window
                Bitmap windowBitmap = new Bitmap(width, height);
                Graphics windowGraphics = Graphics.FromImage(windowBitmap);
                IntPtr windowHdc = windowGraphics.GetHdc();

                // Try to capture the window content
                bool success = PrintWindow(foregroundWindow, windowHdc, 0);

                windowGraphics.ReleaseHdc(windowHdc);
                windowGraphics.Dispose();

                if (!success || IsImageEmpty(windowBitmap))
                {
                    windowBitmap.Dispose();
                    CaptureScreenRegion(windowRect);
                    return;
                }

                // Add to screenshots list
                screenshots.Add(windowBitmap);

                // Extract text from the image and add to extractedTextAll
                string extractedText = ExtractTextFromImage(windowBitmap);
                if (!string.IsNullOrWhiteSpace(extractedText))
                {
                    extractedTextAll += $"\n--- Screenshot {screenshots.Count} ---\n{extractedText.Trim()}\n";
                    if (geminiResponseLabel.Text == "No analysis generated yet. Take screenshots and press Ctrl+Space to analyze.")
                    {
                        FormatGeminiResponse("Screenshots captured. Press Ctrl+Space to analyze with Gemini AI.");
                    }

                }
                UpdateScreenshotThumbnails();
            }
            catch (Exception ex)
            {
                // Fallback to full screen capture on any error
                CaptureFullScreen();
            }
        }

        private void CaptureFullScreen()
        {
            // Hide overlay if visible
            bool wasVisible = this.Visible;
            if (wasVisible) this.Visible = false;

            System.Threading.Thread.Sleep(100); // Give time for overlay to hide

            Rectangle screenBounds = Screen.PrimaryScreen.Bounds;
            Bitmap screenBitmap = new Bitmap(screenBounds.Width, screenBounds.Height);

            using (Graphics graphics = Graphics.FromImage(screenBitmap))
            {
                graphics.CopyFromScreen(0, 0, 0, 0, screenBounds.Size);
            }

            screenshots.Add(screenBitmap);

            // Extract text from the image and add to extractedTextAll
            string extractedText = ExtractTextFromImage(screenBitmap);
            if (!string.IsNullOrWhiteSpace(extractedText))
            {
                extractedTextAll += $"\n--- Screenshot {screenshots.Count} ---\n{extractedText.Trim()}\n";
                if (geminiResponseLabel.Text == "No analysis generated yet. Take screenshots and press Ctrl+Space to analyze.")
                {
                    FormatGeminiResponse("Screenshots captured. Press Ctrl+Space to analyze with Gemini AI.");
                }

            }
            UpdateScreenshotThumbnails();

            // Restore overlay visibility
            if (wasVisible) this.Visible = true;
        }

        private void CaptureScreenRegion(RECT region)
        {
            // Hide overlay if visible
            bool wasVisible = this.Visible;
            if (wasVisible) this.Visible = false;

            System.Threading.Thread.Sleep(50);

            int width = region.Right - region.Left;
            int height = region.Bottom - region.Top;

            Bitmap regionBitmap = new Bitmap(width, height);
            using (Graphics graphics = Graphics.FromImage(regionBitmap))
            {
                graphics.CopyFromScreen(region.Left, region.Top, 0, 0, new Size(width, height));
            }

            screenshots.Add(regionBitmap);

            // Extract text from the image and add to extractedTextAll
            string extractedText = ExtractTextFromImage(regionBitmap);
            if (!string.IsNullOrWhiteSpace(extractedText))
            {
                extractedTextAll += $"\n--- Screenshot {screenshots.Count} ---\n{extractedText.Trim()}\n";
                if (geminiResponseLabel.Text == "No analysis generated yet. Take screenshots and press Ctrl+Space to analyze.")
                {
                    FormatGeminiResponse("Screenshots captured. Press Ctrl+Space to analyze with Gemini AI.");
                }

            }
            UpdateScreenshotThumbnails();

            if (wasVisible) this.Visible = true;
        }
        public async void SendGeminiRequestManually()
        {
            if (string.IsNullOrWhiteSpace(extractedTextAll))
            {
                // Show message in the overlay
                FormatGeminiResponse("No text extracted yet. Take screenshots first using Ctrl+H.");
                return;
            }

            // Show loading message
            FormatGeminiResponse("Analyzing extracted text with Gemini AI... Please wait.");

            // Send to Gemini API
            string language = GetSelectedLanguage();
            string type = GetSelectedType();
            string aiType = GetSelectedAiType();

            string geminiResponse = await SendRequest(extractedTextAll, language, type, aiType);

            FormatGeminiResponse(geminiResponse);

        }

        private async Task<string> SendRequest(string extractedText, string language, string type, string aiType)
        {
            try
            {
                string prompt;
                if (type.ToLower() == "dsa")
                {
                    prompt = DSA_PROMPT_TEMPLATE
                        .Replace("{extractedText}", extractedText)
                        .Replace("{language}", language);
                }
                else if (type.ToLower() == "aptitude")
                {
                    prompt = APTITUDE_PROMPT_TEMPLATE
                        .Replace("{extractedText}", extractedText);
                }
                else
                {
                    prompt = DSA_PROMPT_TEMPLATE
                        .Replace("{extractedText}", extractedText)
                        .Replace("{language}", language);
                }

                if (aiType.ToLower() == "gemini")
                {
                    return await SendGeminiRequest(prompt);
                }
                else if (aiType.ToLower() == "chatgpt")
                {
                    return await SendChatGPTRequest(prompt);
                }
                else
                {
                    return $"Unsupported AI type: {aiType}. Supported types: gemini, chatgpt";
                }
            }
            catch (Exception ex)
            {
                return $"Request failed: {ex.Message}";
            }
        }

        private async Task<string> SendGeminiRequest(string prompt)
        {
            try
            {
                var requestBody = new
                {
                    contents = new[]
                    {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            }
                };

                string jsonRequest = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(GEMINI_API_URL + GEMINI_API_KEY, content);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    dynamic result = JsonConvert.DeserializeObject(jsonResponse);

                    return result?.candidates?[0]?.content?.parts?[0]?.text?.ToString() ?? "No response generated";
                }
                else
                {
                    return $"Gemini API Error: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}";
                }
            }
            catch (Exception ex)
            {
                return $"Gemini request failed: {ex.Message}";
            }
        }

        private async Task<string> SendChatGPTRequest(string prompt)
        {
            try
            {
                var requestBody = new
                {
                    model = "gpt-4.1-mini-2025-04-14", // Updated to use gpt-4.1 as shown in your example
                    input = prompt // Changed from messages array to simple input string
                };

                string jsonRequest = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                // Clear any existing headers and add Authorization header for ChatGPT API
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {CHATGPT_API_KEY}");

                // Updated API endpoint to use /v1/responses instead of /v1/chat/completions
                const string CHATGPT_RESPONSES_URL = "https://api.openai.com/v1/responses";
                var response = await httpClient.PostAsync(CHATGPT_RESPONSES_URL, content);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    dynamic result = JsonConvert.DeserializeObject(jsonResponse);

                    // Updated to parse the new response format
                    // The response contains output array with message objects containing content array
                    var output = result?.output;
                    if (output != null && output.Count > 0)
                    {
                        var message = output[0];
                        var contentArray = message?.content;
                        if (contentArray != null && contentArray.Count > 0)
                        {
                            var textContent = contentArray[0];
                            return textContent?.text?.ToString() ?? "No response generated";
                        }
                    }

                    return "No response generated - unexpected response format";
                }
                else
                {
                    return $"ChatGPT API Error: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}";
                }
            }
            catch (Exception ex)
            {
                return $"ChatGPT request failed: {ex.Message}";
            }
        }

        private void FormatGeminiResponse(string response)
        {
            geminiResponseLabel.Clear();
            geminiResponseLabel.SelectionStart = 0;

            // Split response into lines for processing
            string[] lines = response.Split('\n');

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();

                // Main section headers (1., 2., 3., etc.)
                if (System.Text.RegularExpressions.Regex.IsMatch(trimmedLine, @"^\*\*\d+\.\s"))
                {
                    geminiResponseLabel.SelectionColor = Color.White;
                    geminiResponseLabel.SelectionFont = new Font("Segoe UI", 14, FontStyle.Bold);
                    geminiResponseLabel.AppendText(trimmedLine + "\n");
                }
                // Sub-headers with **text:**
                else if (trimmedLine.StartsWith("**") && trimmedLine.EndsWith("**") ||
                         (trimmedLine.StartsWith("**") && trimmedLine.Contains(":**")))
                {
                    geminiResponseLabel.SelectionColor = Color.White;
                    geminiResponseLabel.SelectionFont = new Font("Segoe UI", 12, FontStyle.Bold);
                    geminiResponseLabel.AppendText(trimmedLine + "\n");
                }
                // Code blocks start/end
                else if (trimmedLine.StartsWith("```"))
                {
                    geminiResponseLabel.SelectionColor = Color.LightGray;
                    geminiResponseLabel.SelectionFont = new Font("Consolas", 10, FontStyle.Bold);
                    geminiResponseLabel.AppendText(trimmedLine + "\n");
                }
                // Code content (inside code blocks)
                else if (IsInsideCodeBlock(lines, Array.IndexOf(lines, line)))
                {
                    geminiResponseLabel.SelectionColor = Color.White;
                    geminiResponseLabel.SelectionFont = new Font("Consolas", 10, FontStyle.Regular);
                    geminiResponseLabel.AppendText(line + "\n"); // Use original line to preserve indentation
                }
                // Tables (lines with |)
                else if (trimmedLine.Contains("|") && trimmedLine.Split('|').Length > 2)
                {
                    geminiResponseLabel.SelectionColor = Color.White;
                    geminiResponseLabel.SelectionFont = new Font("Consolas", 10, FontStyle.Regular);
                    geminiResponseLabel.AppendText(trimmedLine + "\n");
                }
                // Bullet points or numbered lists
                else if (trimmedLine.StartsWith("- ") || trimmedLine.StartsWith("* ") ||
                         System.Text.RegularExpressions.Regex.IsMatch(trimmedLine, @"^\d+\.\s"))
                {
                    geminiResponseLabel.SelectionColor = Color.White;
                    geminiResponseLabel.SelectionFont = new Font("Segoe UI", 10, FontStyle.Regular);
                    geminiResponseLabel.AppendText(trimmedLine + "\n");
                }
                // Checkmarks and status indicators
                else if (trimmedLine.Contains("✅") || trimmedLine.Contains("❌") || trimmedLine.Contains("⚠️"))
                {
                    geminiResponseLabel.SelectionColor = Color.White;
                    geminiResponseLabel.SelectionFont = new Font("Segoe UI", 10, FontStyle.Bold);
                    geminiResponseLabel.AppendText(trimmedLine + "\n");
                }
                // Normal text
                else
                {
                    geminiResponseLabel.SelectionColor = Color.White;
                    geminiResponseLabel.SelectionFont = new Font("Segoe UI", 10, FontStyle.Regular);
                    geminiResponseLabel.AppendText(line + "\n"); // Use original line to preserve spacing
                }
            }

            // Scroll to top
            geminiResponseLabel.SelectionStart = 0;
            geminiResponseLabel.ScrollToCaret();
        }

        private bool IsInsideCodeBlock(string[] lines, int currentIndex)
        {
            int codeBlockStart = -1;
            int codeBlockEnd = -1;

            // Find the nearest code block boundaries
            for (int i = currentIndex; i >= 0; i--)
            {
                if (lines[i].Trim().StartsWith("```"))
                {
                    codeBlockStart = i;
                    break;
                }
            }

            for (int i = currentIndex + 1; i < lines.Length; i++)
            {
                if (lines[i].Trim() == "```")
                {
                    codeBlockEnd = i;
                    break;
                }
            }

            return codeBlockStart >= 0 && (codeBlockEnd == -1 || currentIndex < codeBlockEnd);
        }

        private bool IsImageEmpty(Bitmap bitmap)
        {
            // Simple check to see if the bitmap is mostly empty/black
            const int sampleSize = 10;
            int nonEmptyPixels = 0;

            for (int x = 0; x < Math.Min(bitmap.Width, sampleSize); x++)
            {
                for (int y = 0; y < Math.Min(bitmap.Height, sampleSize); y++)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    if (pixel.R > 10 || pixel.G > 10 || pixel.B > 10)
                    {
                        nonEmptyPixels++;
                    }
                }
            }

            return nonEmptyPixels < 3; // If less than 3 non-black pixels in sample, consider empty
        }

        private void UpdateScreenshotThumbnails()
        {
            // Clear existing thumbnails
            screenshotPanel.Controls.Clear();

            // Add thumbnails for each screenshot
            for (int i = 0; i < screenshots.Count; i++)
            {
                PictureBox thumbnail = new PictureBox();
                thumbnail.Size = new Size(THUMBNAIL_SIZE, THUMBNAIL_SIZE);
                thumbnail.SizeMode = PictureBoxSizeMode.Zoom;
                thumbnail.Image = CreateThumbnail(screenshots[i]);
                thumbnail.BorderStyle = BorderStyle.FixedSingle;
                thumbnail.BackColor = Color.White;
                thumbnail.Margin = new Padding(2);

                // Add click event to view full image
                int index = i; // Capture index for lambda
                thumbnail.Click += (s, e) => ViewFullScreenshot(index);

                screenshotPanel.Controls.Add(thumbnail);
            }

            // Update panel visibility
            screenshotPanel.Visible = screenshots.Count > 0;
        }

        private Image CreateThumbnail(Image originalImage)
        {
            if (originalImage == null) return null;

            Bitmap thumbnail = new Bitmap(THUMBNAIL_SIZE, THUMBNAIL_SIZE);
            using (Graphics graphics = Graphics.FromImage(thumbnail))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(originalImage, 0, 0, THUMBNAIL_SIZE, THUMBNAIL_SIZE);
            }
            return thumbnail;
        }
