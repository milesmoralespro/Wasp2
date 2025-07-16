using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.IO;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Tesseract;

namespace HotkeyOverlayApp
{
    public partial class OverlayForm : Form
    {
        // Windows API imports for global hotkeys
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        // Windows 10/11 API to exclude from screen capture
        [DllImport("user32.dll")]
        private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        // Windows API for layered windows
        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, uint dwAttribute, ref uint pvAttribute, uint cbAttribute);

        // Additional Windows API for screenshot functionality
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

        // Windows API for hit testing
        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT pt);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        // Constants for hotkey registration
        private const int HOTKEY_ID = 9000;
        private const int HOTKEY_ID_UP = 9001;
        private const int HOTKEY_ID_DOWN = 9002;
        private const int HOTKEY_ID_LEFT = 9003;
        private const int HOTKEY_ID_RIGHT = 9004;
        private const int HOTKEY_ID_SCREENSHOT = 9005;
        private const int HOTKEY_ID_CLEAR = 9006;
        private const int HOTKEY_ID_SEND_REQUEST = 9011;
        private const int HOTKEY_ID_TOGGLE_SELECTION = 9007;
        private const int HOTKEY_ID_CYCLE_VALUES = 9008;
        private const int HOTKEY_ID_VIEW_TEXT = 9009;
        private const uint VK_R = 0x52;

        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_SHIFT = 0x0004;
        private const uint VK_B = 0x42;
        private const uint VK_UP = 0x26;
        private const uint VK_DOWN = 0x28;
        private const uint VK_LEFT = 0x25;
        private const uint VK_RIGHT = 0x27;
        private const uint VK_H = 0x48;
        private const uint VK_SPACE = 0x20;
        private const uint VK_D = 0x44;
        private const uint VK_S = 0x53;
        private const uint VK_T = 0x54;

        // Constants for window positioning
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;

        // Constants for window properties
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOOLWINDOW = 0x80;
        private const int WS_EX_NOACTIVATE = 0x8000000;

        // Constants for layered window attributes
        private const uint LWA_ALPHA = 0x2;
        private const uint LWA_COLORKEY = 0x1;

        // Constants for display affinity (screen capture exclusion)
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;
        private const uint WDA_NONE = 0x00000000;

        // DWM attributes for Windows 11 screen capture exclusion
        private const uint DWMWA_EXCLUDED_FROM_PEEK = 12;
        private const uint DWMWA_CLOAK = 13;
        private const uint DWMWA_WINDOW_CORNER_PREFERENCE = 33;

        // Hit test constants
        private const int WM_NCHITTEST = 0x0084;
        private const int HTTRANSPARENT = -1;
        private const int HTCLIENT = 1;

        private bool isVisible = false;
        private int moveStep = 50; // Pixels to move per arrow key press
        private Point currentPosition = new Point(100, 100); // Default position

        // Screenshot functionality
        private List<Image> screenshots = new List<Image>();
        private FlowLayoutPanel screenshotPanel;
        private const int THUMBNAIL_SIZE = 80;

        // Language and Type arrays and selection
        private string[] languageOptions = { "python", "c++", "java", "mysql", "javascript", "reasoning" };
        private string[] typeOptions = { "dsa", "aptitude" };
        private string[] aiTypeOptions = { "gemini", "chatgpt" };
        private int selectedAiTypeIndex = 0; // Default to "gemini" (index 0)

        private int selectedLanguageIndex = 1; // Default to "c++" (index 1)
        private int selectedTypeIndex = 0; // Default to "dsa" (index 0)

        private int selectedOptionType = 0; // 0 = language, 1 = type, 2 = ai_type

        // Display labels for language and type
        private Label languageDisplayLabel;
        private Label typeDisplayLabel;
        private Label aiTypeDisplayLabel;

        // Interactive areas (just screenshots now)
        private List<Control> interactiveControls = new List<Control>();

        // OCR functionality
        private string extractedTextAll = ""; // Single variable to store all extracted text
        private Label extractedTextPreviewLabel;
        private TesseractEngine ocrEngine;

        // Gemini API functionality
        private static readonly HttpClient httpClient = new HttpClient();
        private const string GEMINI_API_KEY = "KEY_HERE"; // Replace with your actual API key
        private const string GEMINI_API_URL = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key=";
        // ChatGPT API functionality
        private const string CHATGPT_API_KEY = "KEY_HERE"; // Replace with your actual OpenAI API key
        private const string CHATGPT_API_URL = "https://api.openai.com/v1/chat/completions";
        private RichTextBox geminiResponseLabel;
        private const string DSA_PROMPT_TEMPLATE = @"You are in a coding interview. Think through this problem naturally, starting with the obvious approach and then optimizing. Show your complete thought process as if you're explaining your reasoning out loud to an interviewer. 
        **CRITICAL: Before providing any code solution, mentally test it with the given examples and edge cases to ensure it compiles and runs correctly. If you find any errors, fix them before presenting the final solution.**

        PROBLEM:
        {extractedText}

        PROVIDE EXACTLY 5 SECTIONS:

        **1. MY INTERVIEW WALKTHROUGH:**
        Start with: ""Let me understand this problem step by step...""
        - Break down what the problem is asking
        - ""My first instinct is to..."" (mention the naive/obvious approach)
        - ""This approach would work, but I'm thinking about the time complexity... it's O(?) which might not be optimal for large inputs""
        - ""Let me think of a better way... What if I..."" (walk through your optimization thinking)
        - ""I also need to consider edge cases like..."" (identify ALL possible edge cases)
        - ""For each edge case, I'll handle it by..."" (explain solutions for each edge case)
        - ""So my final optimized approach will be..."" (conclude with your best solution)

        **2. NAIVE/BRUTE FORCE SOLUTION ({language}):**
        **TESTING NOTE: This solution has been mentally tested with sample inputs to ensure correctness**
        Provide complete working {language} code for the obvious approach with detailed comments and error handling:
        ```{language}
        // Complete naive solution here
        // EVERY single line must have a comment like this:
        // This line does X - Time: O(?), Space: O(?)
        // Method explanation: what this method does and why we need it
        // Include proper error handling and input validation
        ```

        **Analysis of Naive Solution:**
        - Total Time Complexity: O(?)
        - Total Space Complexity: O(?)
        - Why this approach works but isn't optimal
        - **Testing Status: ✅ Verified with sample inputs**

        **3. OPTIMIZED SOLUTION ({language}):**
        **TESTING NOTE: This solution has been mentally tested with sample inputs and edge cases to ensure correctness**

        Provide complete optimized {language} code with detailed comments and optimization explanations.
        ```{language}
        // Complete optimized solution here  
        // EVERY single line must have a comment like this:
        // This line does X - Time: O(?), Space: O(?)
        // Method explanation: what this method does and why we need it
        // Optimization note: why this is better than naive approach
        // Include proper error handling and input validation
        ```

        **Analysis of Optimized Solution:**
        - Total Time Complexity: O(?)
        - Total Space Complexity: O(?)
        - Why this optimization works and how it improves performance
        - **Testing Status: ✅ Verified with sample inputs and edge cases**

        **4. COMPLEXITY COMPARISON & ANALYSIS:**
        Compare both approaches with time/space complexity analysis.
        | Approach | Time Complexity | Space Complexity | Pros | Cons | Testing Status |
        |----------|----------------|------------------|------|------|----------------|
        | Naive    | O(?)           | O(?)             | ?    | ?    | ✅ Tested      |
        | Optimized| O(?)           | O(?)             | ?    | ?    | ✅ Tested      |

        **When to use which approach:**
        - Use naive when...
        - Use optimized when...

        **Code Quality Checklist:**
        - ✅ Handles all edge cases
        - ✅ Includes input validation
        - ✅ Proper error handling
        - ✅ No compilation errors
        - ✅ No runtime errors with sample inputs
        - ✅ Clear variable names and comments

        **5. COMPREHENSIVE EXAMPLE WALKTHROUGHS:**
        Provide detailed examples showing both approaches with verification.

        **Example 1 (Normal Case):**
        Input: [provide a typical input]
        Expected Output: [expected result]

        *Naive Approach Execution:*
        - Step 1: [show variable states and any error checks]
        - Step 2: [show how variables change]
        - Continue until completion...
        - **Verification: ✅ Output matches expected result**

        *Optimized Approach Execution:*
        - Step 1: [show variable states and any error checks]  
        - Step 2: [show how variables change]
        - Continue until completion...
        - **Verification: ✅ Output matches expected result**

        **Example 2 (Edge Case):**
        Input: [provide an edge case]
        Expected Output: [expected result]
        [Trace through both approaches showing how edge cases are handled]
        - **Verification: ✅ Both solutions handle edge case correctly**

        **Example 3 (Another Important Case):**
        Input: [provide another test case that covers different scenarios]
        Expected Output: [expected result]
        [Trace through both approaches]
        - **Verification: ✅ Both solutions produce correct output**

        **FINAL TESTING SUMMARY:**
        - ✅ All solutions compile without errors
        - ✅ All solutions handle provided test cases correctly
        - ✅ All edge cases are properly managed
        - ✅ No runtime exceptions or crashes
        - ✅ Code follows best practices and is production-ready

        **6. DATA STRUCTURE DEEP DIVE ANALYSIS:**
        **Data Structures Identified in Solutions:**
        [List all data structures used in both naive and optimized solutions]

        For each data structure found, provide the following comprehensive analysis:

        **Data Structure: [Name]**

        **📋 What It Does:**
        - Primary purpose and functionality
        - How it organizes and stores data
        - Key characteristics that make it unique

        **⚡ Time & Space Complexity Analysis:**
        | Operation | Time Complexity | Space Complexity | Notes |
        |-----------|----------------|------------------|-------|
        | Insert    | O(?)           | O(?)             | ?     |
        | Delete    | O(?)           | O(?)             | ?     |
        | Search    | O(?)           | O(?)             | ?     |
        | Access    | O(?)           | O(?)             | ?     |
        | Traversal | O(?)           | O(?)             | ?     |

        **🎯 Usage in Our Solutions:**
        - **Naive Solution:** How and why this data structure is used
        - **Optimized Solution:** How and why this data structure is used
        - **Role:** What specific problem it solves in our context

        **🏗️ Internal Implementation Explained:**
        - **Core Structure:** How it's implemented internally (arrays, pointers, nodes, etc.)
        - **Memory Layout:** How data is arranged in memory
        - **Key Algorithms:** Internal algorithms for main operations
        - **Underlying Data Structures:** If this structure uses other data structures internally, explain those too:
          - Sub-structure 1: [Explain its role and implementation]
          - Sub-structure 2: [Explain its role and implementation]

        **💡 Easier Implementation Explanation:**
        - **Simple Analogy:** Real-world analogy to understand the concept
        - **Step-by-Step Breakdown:** How to implement it from scratch
        - **Visual Representation:** Describe how it looks conceptually
        - **Common Pitfalls:** What to watch out for when implementing

        **🌍 Real-World Applications:**
        - Where this data structure is commonly used
        - Industries and systems that rely on it
        - Examples of popular software/algorithms that use it
        - When to choose this over alternatives

        **⚖️ Trade-offs & Alternatives:**
        - **Advantages:** What makes this data structure good
        - **Disadvantages:** Limitations and drawbacks
        - **Alternatives:** Other data structures that could be used instead
        - **Decision Factors:** When to use this vs alternatives

        **🔧 Implementation Variations:**
        - Different ways this data structure can be implemented
        - Language-specific optimizations
        - Memory vs speed trade-offs in different implementations

        ---

        **DATA STRUCTURE SUMMARY TABLE:**
        | Data Structure | Primary Use Case | Best Time Complexity | Worst Time Complexity | Space Complexity | When to Use |
        |---------------|------------------|---------------------|----------------------|------------------|-------------|
        | [Structure 1] | ?                | O(?)                | O(?)                 | O(?)             | ?           |
        | [Structure 2] | ?                | O(?)                | O(?)                 | O(?)             | ?           |

        **🎓 Key Takeaways for Interview:**
        - Most important points about each data structure
        - Common interview questions about these structures
        - How to explain these concepts clearly to an interviewer
        - Red flags to avoid when discussing these data structures

        Make sure all code is production-ready, handles all edge cases, includes proper error handling, and has been mentally tested for correctness. The explanation should sound natural and conversational, as if you're actually thinking through the problem during an interview.
        ";
        private const string APTITUDE_PROMPT_TEMPLATE = @"You are helping someone solve an aptitude problem. Think through this problem step by step, showing your reasoning process clearly.

        **CRITICAL: Before providing any solution, carefully analyze the problem and verify your calculations. If options are provided, you MUST verify which option matches your solution and explain any discrepancies.**

        PROBLEM:
        {extractedText}

        PROVIDE EXACTLY 5 SECTIONS:

        **1. PROBLEM ANALYSIS & OPTIONS DETECTION:**
        Start with: ""Let me understand what this problem is asking...""
        - Break down the problem statement clearly
        - Identify what we need to find
        - **Options Detection:** 
          - ""I can see the following options provided: [list options if present]""
          - OR ""No specific options are provided, so I'll solve and provide the exact answer""
        - ""My approach will be to..."" (explain your strategy)
        - ""I need to be careful about..."" (mention potential pitfalls)

        **2. STEP-BY-STEP SOLUTION:**
        **VERIFICATION NOTE: This solution has been carefully checked for accuracy**

        Provide detailed step-by-step solution:
        - Step 1: [Clear explanation of first step with calculations]
        - Step 2: [Clear explanation of second step with calculations] 
        - Step 3: [Continue with all necessary steps...]
        - **My Calculated Answer:** [Clear final numerical/logical result]

        **Initial Verification Check:**
        - ✅ All calculations double-checked
        - ✅ Logic is sound and follows mathematical principles
        - ✅ Units and context make sense

        **3. OPTION MATCHING & VERIFICATION:**

        **If Options Were Provided:**
        - **My Solution:** [Your calculated answer]
        - **Comparing with given options:**
          - Option A/1/i: [value] - ❌/✅ [Matches/Doesn't match] because [reason]
          - Option B/2/ii: [value] - ❌/✅ [Matches/Doesn't match] because [reason]
          - Option C/3/iii: [value] - ❌/✅ [Matches/Doesn't match] because [reason]
          - [Continue for all options...]

        **If EXACT match found:**
        - ✅ **PERFECT MATCH:** Option [X] = [value] matches my calculated answer exactly
        - **Final Answer:** Option [X]

        **If NO exact match found:**
        - ⚠️ **DISCREPANCY DETECTED:** My answer [value] doesn't exactly match any option
        - **Closest Options Analysis:**
          - Option [X]: [value] - Difference: [±difference] - Possible reasons: [rounding, approximation, etc.]
          - Option [Y]: [value] - Difference: [±difference] - Possible reasons: [different approach, etc.]
        - **Recommended Choice:** Option [X] because [detailed reasoning]

        **If No Options Provided:**
        - **Final Answer:** [Your calculated exact answer with appropriate units/context]

        **4. REVERSE VERIFICATION (TRIAL & ERROR CHECK):**
        **Working backwards from options to verify correctness:**

        **Method: Plugging options back into the problem**
        - **Testing Option A/1/i:** [Show if this option satisfies the original problem conditions]
          - Calculation check: [step-by-step verification]
          - Result: ✅ Works / ❌ Doesn't work because [reason]

        - **Testing Option B/2/ii:** [Show if this option satisfies the original problem conditions]
          - Calculation check: [step-by-step verification]  
          - Result: ✅ Works / ❌ Doesn't work because [reason]

        [Continue for all options...]

        **Reverse Verification Conclusion:**
        - ✅ Option [X] passes reverse verification
        - ❌ Options [Y, Z] fail because [specific reasons]
        - **This confirms our forward calculation was correct**

        **5. ALTERNATIVE APPROACHES & COMPREHENSIVE VERIFICATION:**

        **Alternative Method 1:**
        - **Approach:** [Different mathematical or logical approach]
        - **Steps:** 
          - Step 1: [calculation]
          - Step 2: [calculation]
          - **Result:** [answer]
        - **Verification:** ✅ Matches our primary solution / ⚠️ Shows slight difference of [value]

        **Alternative Method 2:**
        - **Approach:** [Another different approach if applicable]
        - **Steps:** [detailed steps]
        - **Result:** [answer]
        - **Verification:** [comparison with other methods]

        **COMPREHENSIVE ERROR ANALYSIS:**
        **Common mistakes in this type of problem:**
        - Mistake 1: [What people commonly get wrong and why]
        - Mistake 2: [Another common error and its consequences]
        - **How I avoided these:** [Prevention strategies used]

        **FINAL ANSWER VERIFICATION:**
        - ✅ Forward calculation completed and verified
        - ✅ Reverse verification confirms the answer
        - ✅ Alternative methods support the conclusion
        - ✅ Answer matches Option [X] / Exact answer is [value]
        - ✅ All edge cases and assumptions considered

        **DECISION CONFIDENCE:**
        - **Confidence Level:** [High/Medium/Low] 
        - **Reasoning:** [Why you're confident/uncertain]
        - **If uncertain:** ""The closest reasonable choice is Option [X] because [detailed reasoning about why this is the best available choice despite any discrepancies]""

        **KEY CONCEPTS & FORMULAS USED:**
        - Concept 1: [Explanation and when to use]
        - Formula 1: [Formula and application]
        - Concept 2: [Explanation and application]

        **FINAL RECOMMENDED ANSWER:**
        **If multiple choice:** Option [X] - [value/description]
        **If no options:** [Exact calculated answer with units/context]
        **Justification:** [Brief summary of why this is the correct choice]

        Make sure all calculations are accurate, verify against provided options, and explain any discrepancies clearly. If no exact match exists among options, choose the closest reasonable answer and explain the reasoning thoroughly.
        ";

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
