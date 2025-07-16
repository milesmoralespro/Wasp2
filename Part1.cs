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
        private const string CHATGPT_API_KEY = "KEY_HERER"; // Replace with your actual OpenAI API key
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

