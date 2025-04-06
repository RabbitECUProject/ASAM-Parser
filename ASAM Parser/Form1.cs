using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace ASAM_Tool
{
    public partial class ASAMParser : Form
    {
        string mszSourcePath;
        string mszOutputPath;
        string mszLinkerFilePath;
        string mszSRecordPath;
        string mszUserCalPatchPath;
        string mszCalStructName;
        string[] maszDirectorySourceFiles;
        string[] maszDataTypes;
        byte[] mau8SRecBuff;
        public BackgroundWorker mMakeA2LWorker;
        UInt32 mu32SRecBaseAddress = 0xffffffff;
        List<String> mlstASAMStrings;
        List<tclsASAMElement> maclsASAMElements;
        List<tstTypeDef> mlsttstTypedefs;
        List<string> mlstPatchFileStrings;
        UInt32 mu32CalStructOffsetsTableAddress = 0xffffffff;
        UInt32 mu32CalStructAddress = 0xffffffff;

        public ASAMParser()
        {
            InitializeComponent();
            mlstASAMStrings = new List<string>();
            maclsASAMElements = new List<tclsASAMElement>();
            mlstPatchFileStrings = new List<string>();
            mlsttstTypedefs = new List<tstTypeDef>();
            mau8SRecBuff = new byte[65536 * 8];
            
            tstTypeDef stTypedef;
            stTypedef.szDataType = "uint8";
            stTypedef.szTypeAlias = "bool";

            mlsttstTypedefs.Add(stTypedef);


            maszDataTypes = new String[6] {"uint8", "sint8", "uint16", "sint16", "uint32", "sint32"};

            mMakeA2LWorker = new BackgroundWorker();
            mMakeA2LWorker.DoWork += new DoWorkEventHandler(MakeA2L);
        }

        private void ASAMParser_Load(object sender, EventArgs e)
        {
            tclsIniParser mclsIniParser = new tclsIniParser("c:\\MDAC\\ECUHOST\\Dev Tools\\ASAM Tool.INI");

            try
            {
                mszLinkerFilePath = mclsIniParser.GetSetting("Paths", "Linker");
                mszSourcePath = mclsIniParser.GetSetting("Paths", "Source");
                mszSRecordPath = mclsIniParser.GetSetting("Paths", "SRecord");
                mszUserCalPatchPath = mclsIniParser.GetSetting("Paths", "UserCalPatch");
                mszCalStructName = mclsIniParser.GetSetting("Structures", "CalStruct");
            }
            catch
            {
                mszLinkerFilePath = "Unknown";
                mszSourcePath = "Unknown"; 
            }
        }

        private void OutputASAM()
        {
            using (StreamWriter clsSW = new StreamWriter(mszOutputPath + "\\ECUOUT.A2L"))

            foreach (tclsASAMElement clsASAMElement in maclsASAMElements)
            {
                string[] aszOutputStrings = clsASAMElement.aszGetOutput();

                foreach (string szOutput in aszOutputStrings)
                {
                    if (null != szOutput)
                    {
                        clsSW.WriteLine(szOutput);
                    }
                    else
                    {
                        break;
                    }
                }

                clsSW.WriteLine("");
                clsSW.WriteLine("");
            }
        }

        private void GetAllFiles()
        {
            maszDirectorySourceFiles = Directory.GetFiles(mszSourcePath, "*.c", SearchOption.AllDirectories);
            int iOldLength = maszDirectorySourceFiles.Length;
            String[] aszHeaderFiles = Directory.GetFiles(mszSourcePath, "*.h", SearchOption.AllDirectories);
            Array.Resize(ref maszDirectorySourceFiles, aszHeaderFiles.Length + iOldLength);
            aszHeaderFiles.CopyTo(maszDirectorySourceFiles, iOldLength);
        }

        private void ParseLinkerFile()
        {
            int iAddressFoundCount = 0;
            int iLineCount = 0;

            using (StreamReader clsSR = new StreamReader(mszLinkerFilePath))

            while (0 <= clsSR.Peek())
            {
                string szInputLine = clsSR.ReadLine();
                string[] aszLinkerLineStrings;

                iLineCount++;

                szInputLine = szInputLine.Trim();

                while (szInputLine.Contains("  "))
                {
                    szInputLine = szInputLine.Replace("  ", " ");
                }

                aszLinkerLineStrings = szInputLine.Split(' ');

                foreach (tclsASAMElement clsElement in maclsASAMElements)
                {
                    foreach (string szArrayString in aszLinkerLineStrings)
                    {
                        string szSubString = szArrayString.Trim();

                        //if (szSubString == clsElement.mszVarName)
                        if (szInputLine.Contains(clsElement.mszVarName))
                        {
                              //if (szInputLine.Contains("0x"))
                            if (szArrayString.Contains("0x"))
                            {
                                int iCharPos = szArrayString.IndexOf("0x");

                                if (0 == iCharPos)
                                {
                                    string szSearchString = szArrayString.Substring(iCharPos + 2, szArrayString.Length - 2);
                                    szSearchString = szSearchString.Trim();

                                    if (8 == szSearchString.Length)
                                    {
                                        UInt32 u32Address = UInt32.Parse(szSearchString, System.Globalization.NumberStyles.HexNumber);
                                        clsElement.vSetAddress(u32Address);

                                        if (clsElement.mszVarName.Contains("au32Offsets"))
                                        {
                                            mu32CalStructOffsetsTableAddress = u32Address;
                                        }

                                        iAddressFoundCount++;
                                    }
                                }
                            }
                        }
                    }
                }

                if (szInputLine.Contains(mszCalStructName))
                {
                    foreach (string szArrayString in aszLinkerLineStrings)
                    {
                        string szSubString = szArrayString.Trim();

                        if (szArrayString.Contains("0x"))
                        {
                            int iCharPos = szArrayString.IndexOf("0x");
                            string szSearchString = szArrayString.Substring(iCharPos + 2, szArrayString.Length - 2);
                            szSearchString = szSearchString.Trim();

                            if (8 == szSearchString.Length)
                            {
                                mu32CalStructAddress = UInt32.Parse(szSearchString, System.Globalization.NumberStyles.HexNumber);
                            }
                        }
                    }
                }
            }

            MessageBox.Show("Linker addresses added for " + Convert.ToString(iAddressFoundCount) +
                " non struct objects of " + Convert.ToString(maclsASAMElements.Count) + " total objects");
        }

        private void LoadPatchFile()
        {
            mlstPatchFileStrings.Clear();

            using (StreamReader clsSR = new StreamReader(mszUserCalPatchPath))

            while (0 <= clsSR.Peek())
            {
                string szInputLine = clsSR.ReadLine();

                mlstPatchFileStrings.Add(szInputLine);
            }
        }

        private void PatchFileOffsets()
        {
            List<int> lstASAMStringIndices = new List<int>();
            int iIDX = 0;
            int iPatchCount;
            int iPatchedCount = 0;
            string szOldOffset;
            string szNewOffset;

            foreach (string szPatchString in mlstPatchFileStrings)
            {
                if (szPatchString.Contains("//ASAM"))
                {
                    lstASAMStringIndices.Add(iIDX);
                }

                iIDX++;
            }

            byte[] au32Offsets = new byte[lstASAMStringIndices.Count * 4];
            au32Offsets = GetByteArrayFromSRec(mu32CalStructOffsetsTableAddress - mu32SRecBaseAddress, au32Offsets.Length);

            iPatchCount = iIDX;

            for (iIDX = 0; iIDX < lstASAMStringIndices.Count; iIDX++)
            {
                string[] aszSubStrings = mlstPatchFileStrings[lstASAMStringIndices[iIDX]].Split();

                foreach (string szToken in aszSubStrings)
                {
                    if (szToken.Contains("offset="))
                    {
                        UInt32 u32Offset;
                        szOldOffset = szToken.Trim();

                        u32Offset = (UInt32)au32Offsets[4 * iIDX + 0] + 0x100 * (UInt32)au32Offsets[4 * iIDX + 1] +
                            0x10000 * (UInt32)au32Offsets[4 * iIDX + 2] + 0x1000000 * (UInt32)au32Offsets[4 * iIDX + 3];

                        szNewOffset = "offset=" + Convert.ToString(u32Offset);
                        mlstPatchFileStrings[lstASAMStringIndices[iIDX]] = mlstPatchFileStrings[lstASAMStringIndices[iIDX]].Replace(szOldOffset, szNewOffset);
                        iPatchedCount++;
                    }
                }
            }

            MessageBox.Show("Offets patched = " + iPatchedCount.ToString() + ", of total offets = " + iPatchCount.ToString());
        }

        private void WritePatchFile()
        {
            using (StreamWriter clsSW = new StreamWriter(mszUserCalPatchPath))

            foreach (string szOutLine in mlstPatchFileStrings)
            {
                if (null != szOutLine)
                {
                    clsSW.WriteLine(szOutLine);
                }
            }

            MessageBox.Show("Calibration header was patched at " + mszUserCalPatchPath, "User calibration file patched");
        }

        private void ReplaceASAMStringTypedefs()
        {
            int ASAMStringIDX = 0;
            string szASAMString;

            for (ASAMStringIDX = 0; ASAMStringIDX < mlstASAMStrings.Count; ASAMStringIDX++)
            {
                szASAMString = mlstASAMStrings[ASAMStringIDX];

                foreach (tstTypeDef stTypedef in mlsttstTypedefs)
                {
                    if (szASAMString.Contains(stTypedef.szTypeAlias))
                    {
                        mlstASAMStrings[ASAMStringIDX] = szASAMString.Replace(stTypedef.szTypeAlias,
                            stTypedef.szDataType);
                    }
                }
            }
        }

        private void ProcessASAMStrings()
        {
            tclsASAMElement clsASAMElement;
            string szASAMDataType = "";
            string szASAMName = "";
            string szASAMGroup = "";
            string szASAMHelp = "";
            string szASAMMode = "";
            string szASAMUnits = "";
            string szASAMFormat = "";
            string szVariableName = "";
            string szSearchString = "";
            string szTemp;
            string szASAMMin = "";
            string szASAMMax = "";
            string szASAMm = "";
            string szASAMb = "";
            string szXIndexVar = "";
            string szYIndexVar = "";
            string szXCount = "";
            string szYCount = "";
            string szPointCount = "";
            string szParentStruct = "";
            string[] aszTemp;
            int iByteCount = 0;
            int iByteOffset = 0;
            int iCharPos;
            int iCharPos2;

            foreach (string szASAMString in mlstASAMStrings)
            {
                szASAMName = szGetParameterEquals("name", szASAMString, true);
                szASAMHelp = szGetParameterEquals("help", szASAMString, true);
                szASAMMin = szGetParameterEquals("min", szASAMString, false);
                szASAMMax = szGetParameterEquals("max", szASAMString, false);
                szASAMUnits = szGetParameterEquals("units", szASAMString, true);
                szASAMFormat = szGetParameterEquals("format", szASAMString, false);
                szASAMm = szGetParameterEquals("m", szASAMString, false);
                szASAMb = szGetParameterEquals("b", szASAMString, false);
                szASAMMode = szGetParameterEquals("mode", szASAMString, false);
                szTemp = szGetParameterEquals("offset", szASAMString, false);
                szXCount = szGetParameterEquals("xcount", szASAMString, false);
                szYCount = szGetParameterEquals("ycount", szASAMString, false);
                szXIndexVar = szGetParameterEquals("xindexvar", szASAMString, true);
                szYIndexVar = szGetParameterEquals("yindexvar", szASAMString, true);
                szPointCount = szGetParameterEquals("pointcount", szASAMString, false);
                szParentStruct = szGetParameterEquals("parent", szASAMString, true);

                try
                {
                    iByteOffset = Convert.ToInt32(szTemp);
                }
                catch
                {
                    iByteOffset = 0;
                }


                if (szASAMString.Contains("EXTERN"))
                {
                    iCharPos = szASAMString.IndexOf("EXTERN");

                    if (-1 < iCharPos)
                    {
                        szSearchString = szASAMString.Substring(iCharPos + 6);
                    }
                    else
                    {
                        szSearchString = "no_declaration_found ";
                    }

                    szSearchString = szSearchString.Trim();

                    if (szSearchString.Contains("="))
                    {
                        iCharPos = szSearchString.IndexOf("=");
                        szSearchString = szSearchString.Substring(0, iCharPos);
                    }

                    if (szSearchString.Contains("__attribute__"))
                    {
                        szSearchString = szSearchString.Replace("__attribute__", "");
                    }

                    if (szSearchString.Contains("__ATTRIBUTE__"))
                    {
                        szSearchString = szSearchString.Replace("__ATTRIBUTE__", "");
                    }

                    if (szSearchString.Contains("const"))
                    {
                        szSearchString = szSearchString.Replace("const", "");
                    }

                    if (szSearchString.Contains("CONST"))
                    {
                        szSearchString = szSearchString.Replace("CONST", "");
                    }

                    if (szSearchString.Contains("volatile"))
                    {
                        szSearchString = szSearchString.Replace("volatile", "");
                    }

                    if (szSearchString.Contains("VOLATILE"))
                    {
                        szSearchString = szSearchString.Replace("VOLATILE", "");
                    }

                    if (szSearchString.Contains("(("))
                    {
                        szSearchString = szSearchString.Replace("((", "(");
                    }

                    if (szSearchString.Contains("))"))
                    {
                        szSearchString = szSearchString.Replace("))", ")");
                    }

                    if (szSearchString.Contains("("))
                    {
                        iCharPos = szSearchString.IndexOf('(');
                        iCharPos2 = szSearchString.IndexOf(')');

                        if (iCharPos2 > iCharPos)
                        {
                            string szReplace = szSearchString.Substring(iCharPos, iCharPos2 - iCharPos + 1);
                            szSearchString = szSearchString.Replace(szReplace, "");
                            szSearchString = szSearchString.Trim();
                        }
                    }

                    while (szSearchString.Contains("  "))
                    {
                        szSearchString = szSearchString.Replace("  ", " ");
                    }

                    szSearchString = szSearchString.Trim();

                    aszTemp = szSearchString.Split(' ');
                    szSearchString = aszTemp[1];

                    iCharPos = szSearchString.IndexOf('[');

                    if (-1 < iCharPos)
                    {
                        szSearchString = szSearchString.Substring(0, iCharPos);
                    }

                    iCharPos = szSearchString.IndexOf(';');

                    if (-1 < iCharPos)
                    {
                        szSearchString = szSearchString.Substring(0, iCharPos);
                    }

                    szVariableName = szSearchString;

                    iCharPos = szVariableName.IndexOf("_");

                    if (-1 < iCharPos)
                    {
                        szASAMGroup = szSearchString.Substring(0, iCharPos);
                    }
                }

                foreach (string szTypeString in maszDataTypes)
                {
                    if (szASAMString.Contains(szTypeString))
                    {
                        szASAMDataType = szTypeString;

                        switch (szASAMDataType)
                        {
                            case "uint8":
                                iByteCount = 1;
                                break;
                            case "sint8":
                                iByteCount = 1;
                                break;
                            case "uint16":
                                iByteCount = 2;
                                break;
                            case "sint16":
                                iByteCount = 2;
                                break;
                            case "uint32":
                                iByteCount = 4;
                                break;
                            case "sint32":
                                iByteCount = 4;
                                break;
                        }
                    }
                }

                clsASAMElement = new tclsASAMElement(szASAMName, szVariableName, szASAMGroup, szASAMUnits, szASAMFormat, szASAMDataType, szASAMMode, szASAMHelp, iByteCount, iByteOffset, szASAMMin, szASAMMax, szASAMm, szASAMb, szXCount, szYCount, szXIndexVar, szYIndexVar, szPointCount, szParentStruct);
                maclsASAMElements.Add(clsASAMElement);
            }
        }

        private string szGetParameterEquals(string szParamString, string szSearchString, bool boFindString)
        {
            int iCharPos = szSearchString.IndexOf(szParamString + "=");

            if (-1 < iCharPos)
            {
                szSearchString = szSearchString.Substring(iCharPos + szParamString.Length + 1);
            }
            else
            {
                if (true == boFindString)
                {
                    szSearchString = "\"no_" + szParamString + "_found \"";
                }
                else
                {
                    szSearchString = "no_" + szParamString + "_found ";
                }
            }

            if (true == boFindString)
            {
                szSearchString = szSearchString.Substring(1);
                iCharPos = szSearchString.IndexOf('"');
                if (-1 < iCharPos)
                {
                    szSearchString = szSearchString.Substring(0, iCharPos);
                }
            }
            else
            {
                iCharPos = szSearchString.IndexOf(' ');
                if (-1 < iCharPos)
                {
                    szSearchString = szSearchString.Substring(0, iCharPos);
                }
            }

            return szSearchString;
        }

        private void FindASAMStringsAndTypedefs()
        {
            String szDeclarationString = new String(' ', 0);
            String szInputLinePlusDeclaration = new String(' ', 0);

            foreach (String szFileName in maszDirectorySourceFiles)
            {
                using (StreamReader clsSR = new StreamReader(szFileName))

                while (0 <= clsSR.Peek())
                {
                    String szInputLine = clsSR.ReadLine().Trim();

                    if (szInputLine.Contains("ASAM mode="))
                    {
                        if (szDeclarationString.Contains("EXTERN"))
                        {
                            szInputLinePlusDeclaration = szInputLine + " " + szDeclarationString;
                            szInputLinePlusDeclaration = szInputLinePlusDeclaration.Replace('\t', ' ');
                            szInputLinePlusDeclaration = szInputLinePlusDeclaration.Replace("  ", " ");
                            szInputLinePlusDeclaration = szInputLinePlusDeclaration.Replace("  ", " ");
                            szInputLinePlusDeclaration = szInputLinePlusDeclaration.Replace("  ", " ");
                            mlstASAMStrings.Add(szInputLinePlusDeclaration);
                        }
                        else
                        {
                            szInputLinePlusDeclaration = szInputLine + " EXTERN " + szDeclarationString;
                            szInputLinePlusDeclaration = szInputLinePlusDeclaration.Replace('\t', ' ');
                            szInputLinePlusDeclaration = szInputLinePlusDeclaration.Replace("  ", " ");
                            szInputLinePlusDeclaration = szInputLinePlusDeclaration.Replace("  ", " ");
                            szInputLinePlusDeclaration = szInputLinePlusDeclaration.Replace("  ", " ");
                            mlstASAMStrings.Add(szInputLinePlusDeclaration);
                        }
                    }
                    else
                    {
                        if (0 != szInputLine.Length)
                        { 
                            if (!szInputLine.Contains("//"))
                            {
                                szDeclarationString = szInputLine;
                            }
                        }
                    }

                    if (szInputLine.Contains("typedef"))
                    {
                        string[] aszTypedefStrings;

                        aszTypedefStrings = szInputLine.Split(' ');

                        if (aszTypedefStrings[0].Contains("typedef"))
                        {
                            tstTypeDef stTypedef;

                            aszTypedefStrings[1] = aszTypedefStrings[1].Trim();

                            switch (aszTypedefStrings[1])
                            {
                                case "uint8":
                                case "uint16":
                                case "uint32":
                                case "sint8":
                                case "sint16":
                                case "sint32":
                                    stTypedef.szDataType = aszTypedefStrings[1];

                                    if (aszTypedefStrings[2].Contains(";"))
                                    {
                                        aszTypedefStrings[2] = aszTypedefStrings[2].Replace(';', ' ');
                                        aszTypedefStrings[2] = aszTypedefStrings[2].Trim();
                                    }


                                    stTypedef.szTypeAlias = aszTypedefStrings[2];
                                    mlsttstTypedefs.Add(stTypedef);
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
            }
        }

        private void buttonMakeA2L_Click(object sender, EventArgs e)
        {
            if (false == mMakeA2LWorker.IsBusy)
            {
                mMakeA2LWorker.RunWorkerAsync();
            }
        }

        private void MakeA2L(object sender, DoWorkEventArgs e)
        {
            mlstASAMStrings.Clear();
            maclsASAMElements.Clear();
            mlsttstTypedefs.Clear();

            MessageBox.Show("Select the source folder then the project map file", "Select Files");
            mszSourcePath = szGetFolderPath(mszSourcePath);            
            mszLinkerFilePath = szGetFilePath(mszLinkerFilePath, "Map file", "map");  
            mszOutputPath = "C:\\MDAC\\ECUHOST\\Database\\A2L Databases";

            if ((null != mszLinkerFilePath) && (null != mszSourcePath))
            {
                Invoke((MethodInvoker)delegate
                {
                    textBoxProgress.Text += "Opening Files...\r\n";
                });
                GetAllFiles();


                Invoke((MethodInvoker)delegate
                {
                    textBoxProgress.Text += "Finding ASAM Tokens...\r\n";
                });
                FindASAMStringsAndTypedefs();

                Invoke((MethodInvoker)delegate
                {
                    textBoxProgress.Text += "Processing ASAM Tokens...\r\n";
                });
                ReplaceASAMStringTypedefs();
                ProcessASAMStrings();

                Invoke((MethodInvoker)delegate
                {
                    textBoxProgress.Text += "Linking ASAM addresses...\r\n";
                });
                ParseLinkerFile();

                Invoke((MethodInvoker)delegate
                {
                    textBoxProgress.Text += "Linking structure offset addresses...\r\n";
                });
                LinkCalStructOffsets();

                Invoke((MethodInvoker)delegate
                {
                    textBoxProgress.Text += "Writing ASAM A2L file...\r\n";
                });
                OutputASAM();

                Invoke((MethodInvoker)delegate
                {
                    MessageBox.Show("ASAM A2L file was output at " + mszOutputPath, "ASAM A2L file created");
                });
            }
            else
            {
                Invoke((MethodInvoker)delegate
                {
                    MessageBox.Show("Error occurred no ASAM file was output to " + mszOutputPath, "Error occurred");
                });
            }
        }

        private void LinkCalStructOffsets()
        {
            foreach (tclsASAMElement clsElement in maclsASAMElements)
            {
                clsElement.vSetStructAddress(mszCalStructName, mu32CalStructAddress);
            }
        }

        private String szGetFilePath(String szDefault, String szFileType, String szFilter)
        {
            String szFilePath = null;
            OpenFileDialog clsOpenFileDialog = new OpenFileDialog();

            clsOpenFileDialog.Filter = szFileType + "(*." + szFilter + ")|*." + szFilter;

            if ("unknown" != szDefault)
            {
                clsOpenFileDialog.InitialDirectory = szDefault;
            }


            Invoke((MethodInvoker)delegate
            {
                DialogResult result = clsOpenFileDialog.ShowDialog();

                if (DialogResult.OK == result)
                {
                    szFilePath = clsOpenFileDialog.FileName;
                }
            });

            return szFilePath;
        }

        private String szGetFolderPath(String szDefault)
        {
            String szFolderPath = null;
            DialogResult result;
            FolderBrowserDialog clsFolderBrowserDialog = new FolderBrowserDialog();

            if ("unknown" != szDefault)
            {
                clsFolderBrowserDialog.SelectedPath = szDefault;
            }

            Invoke((MethodInvoker)delegate
            {
                result = clsFolderBrowserDialog.ShowDialog();

                if (DialogResult.OK == result)
                {
                    szFolderPath = clsFolderBrowserDialog.SelectedPath;
                }
            });

            return szFolderPath;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if ((null != mszSRecordPath) && (false == mMakeA2LWorker.IsBusy))
            { 
                LoadSRecordArray();

                if (null != mszUserCalPatchPath)
                {
                    LoadPatchFile();
                    PatchFileOffsets();
                    WritePatchFile();
                }
            }
        }

        private void LoadSRecordArray()
        {
            UInt32 u32Address = 0xffffffff;
            bool boErr = false;
            SRecord clsSRec = new ASAM_Tool.SRecord();
            SRecordStructure stSRec = new SRecordStructure();

            using (StreamReader clsSR = new StreamReader(mszSRecordPath))
            {
                while ((stSRec != null) && (false == boErr))
                {
                    stSRec = clsSRec.Read(clsSR);

                    if (null != stSRec)
                    {
                        if (2 == stSRec.type)
                        {
                            if (0xffffffff == u32Address)
                            {
                                u32Address = stSRec.address;
                                mu32SRecBaseAddress = u32Address;
                            }

                            if (u32Address != stSRec.address)
                            {
                                boErr = true;
                            }
                            else
                            {
                                Array.Copy(stSRec.data, 0, mau8SRecBuff, u32Address - mu32SRecBaseAddress, stSRec.dataLen);
                            }

                            u32Address += (UInt32)stSRec.dataLen;
                        }
                    }
                }
            }
        }

        private byte[] GetByteArrayFromSRec(UInt32 u32Address, int iByteCount)
        {
            byte[] au8Data = new byte[iByteCount];

            Array.Copy(mau8SRecBuff, u32Address, au8Data, 0, iByteCount);

            return au8Data;
        }

    }


    public class tclsASAMElement
    {
        public String mszVarName;
        string mszName;
        string mszGroup;
        string mszHelp;
        string mszCompuMethod;
        string mszMode;
        string mszElementType;
        string mszDataType;
        string mszUnits;
        string mszFormat;
        string mszXIndexVar;
        string mszYIndexVar;
        string mszParentStruct;
        int miByteCount;
        int miByteOffset;
        int miXIndexCount;
        int miYIndexCount;
        int miPointCount;
        UInt32 mu32Address;
        Single msElementMin;
        Single msElementMax;
        Single msm;
        Single msb;

        public tclsASAMElement(string szName, string szVarName, string szGroup, string szUnits, string szFormat, string szDataType, string szMode, string szHelp, int iByteCount, int iByteOffset, string szElementMin, string szElementMax, string szm, string szb, string szXCount, string szYCount, string szXIndexVar, string szYIndexVar, string szPointCount, string szParentStruct)
        {
            mszName = szName; 
            mszVarName = szVarName; 
            mszGroup = szGroup;
            mszDataType = szDataType;
            mszHelp = szHelp;
            mszFormat = szFormat;
            mszUnits = szUnits;
            mszXIndexVar = szXIndexVar;
            mszYIndexVar = szYIndexVar;
            mszParentStruct = szParentStruct;
            miByteCount = iByteCount;
            miByteOffset = iByteOffset;

            try
            {
                msElementMin = Single.Parse(szElementMin, System.Globalization.NumberStyles.Float);
            }
            catch
            {
                msElementMin = 0;
            }

            try
            {
                msElementMax = Single.Parse(szElementMax, System.Globalization.NumberStyles.Float);
            }
            catch
            {
                msElementMax = 0;
            }

            try
            {
                msm = Single.Parse(szm, System.Globalization.NumberStyles.Float);
            }
            catch
            {
                msm = 0;
            }

            try
            {
                miXIndexCount = Convert.ToInt32(szXCount);
            }
            catch
            {
                miXIndexCount = 0;
            }

            try
            {
                miYIndexCount =  Convert.ToInt32(szYCount);
            }
            catch
            {
                miYIndexCount = 0;
            }

            try
            {
                miPointCount = Convert.ToInt32(szPointCount);
            }
            catch
            {
                miPointCount = 0;
            }

            switch (szMode)
            {
                case "readvalue":
                    mszMode = "MEASUREMENT";
                    mszElementType = "VALUE";
                    break;
                case "writevalue":
                    mszMode = "CHARACTERISTIC";
                    mszElementType = "VALUE";
                    break;
                case "writecurve":
                    mszMode = "CHARACTERISTIC";
                    mszElementType = "CURVE";
                    break;
                case "writemap":
                    mszMode = "CHARACTERISTIC";
                    mszElementType = "MAP";
                    break;
                case "writeaxis_pts":
                    mszMode = "AXIS_PTS";
                    mszElementType = "AXIS_PTS";
                    break;
                case "writeblob":
                    mszMode = "BLOB";
                    mszElementType = "BLOB";
                    break;
                default:
                    mszMode = "ERROR";
                    break;
            }

            mszCompuMethod = "CM_" + mszName.ToUpper();
            mszCompuMethod = mszCompuMethod.Replace(' ', '_');
        }

        public void vSetAddress(UInt32 u32Address)
        {
            mu32Address = u32Address + (UInt32)miByteOffset;
        }

        public void vSetStructAddress(string szStructName, UInt32 u32StructAddress)
        {
            if (szStructName == mszParentStruct)
            {
                mu32Address = (UInt32)miByteOffset + u32StructAddress;
            }
        }

        public string[] aszGetOutput()
        {
            string[] aszOutputStrings = new string[120];
            string szDataTypeString = "";
            int iLineIndex = 0;

            switch (mszDataType)
            {
                case "uint8":
                    szDataTypeString = "RL_VALU8";
                    break;
                case "sint8":
                    szDataTypeString = "RL_VALS8";
                    break;
                case "uint16":
                    szDataTypeString = "RL_VALU16";
                    break;
                case "sint16":
                    szDataTypeString = "RL_VALS16";
                    break;
                case "uint32":
                    szDataTypeString = "RL_VALU32";
                    break;
                case "sint32":
                    szDataTypeString = "RL_VALS32";
                    break;
            }

            switch (mszElementType)
            {
                case "BLOB":
                    {
                        aszOutputStrings[iLineIndex++] = "/begin " + mszMode + " " + mszName;
                        aszOutputStrings[iLineIndex++] = "\"" + mszHelp + "\"";
                        aszOutputStrings[iLineIndex++] = "BLOB";
                        aszOutputStrings[iLineIndex++] = "0x" + mu32Address.ToString("X8");
                        aszOutputStrings[iLineIndex++] = szDataTypeString;
                        aszOutputStrings[iLineIndex++] = mszGroup;
                        aszOutputStrings[iLineIndex++] = msElementMin.ToString();
                        aszOutputStrings[iLineIndex++] = msElementMax.ToString();
                        aszOutputStrings[iLineIndex++] = miPointCount.ToString();
                        aszOutputStrings[iLineIndex++] = "/end" + " " + mszMode;
                        aszOutputStrings[iLineIndex++] = "";
                        aszOutputStrings[iLineIndex++] = "";
                        break;
                    }
                case "VALUE":
                    {
                        aszOutputStrings[iLineIndex++] = "/begin " + mszMode + " " + mszName;
                        aszOutputStrings[iLineIndex++] = "\"" + mszHelp + "\"";
                        aszOutputStrings[iLineIndex++] = "VALUE";
                        aszOutputStrings[iLineIndex++] = "0x" + mu32Address.ToString("X8");
                        aszOutputStrings[iLineIndex++] = szDataTypeString;
                        aszOutputStrings[iLineIndex++] = mszCompuMethod;
                        aszOutputStrings[iLineIndex++] = mszGroup;
                        aszOutputStrings[iLineIndex++] = msElementMin.ToString();
                        aszOutputStrings[iLineIndex++] = msElementMax.ToString();
                        aszOutputStrings[iLineIndex++] = "/end" + " " + mszMode;
                        aszOutputStrings[iLineIndex++] = "";
                        aszOutputStrings[iLineIndex++] = "";

                        aszOutputStrings[iLineIndex++] = "/begin COMPU_METHOD " + mszCompuMethod;
                        aszOutputStrings[iLineIndex++] = "Conversion for " + "\"" + mszName + "\"";

                        if ((0 == msm) && (0 == msb))
                        {
                            aszOutputStrings[iLineIndex++] = "TAB_VERB %" + mszFormat + " " +" ENUM";
                            String szTemp = mszCompuMethod.Replace("CM_", "CM_TAB_VERB_");
                            aszOutputStrings[iLineIndex++] = "COMPU_TAB_REF " + szTemp;
                            aszOutputStrings[iLineIndex++] = "/end COMPU_METHOD";
                            aszOutputStrings[iLineIndex++] = "";

                            aszOutputStrings[iLineIndex++] = "/begin COMPU_TAB_RANGE " + szTemp;
                            aszOutputStrings[iLineIndex++] = "\"  \"";

                            szTemp = mszUnits.Replace("ENUMERATION", "");
                            szTemp = szTemp.Trim();

                            while (szTemp.Contains("  "))
                            {
                                szTemp = szTemp.Replace("  ", " ");
                            }

                            String[] aszTemp = szTemp.Split(' ');

                            aszOutputStrings[iLineIndex++] = Convert.ToString(aszTemp.Length);

                            foreach (String szEnum in aszTemp)
                            {
                                String[] aszEnumStrings = szEnum.Split('=');
                                aszOutputStrings[iLineIndex++] = aszEnumStrings[1] + " " + aszEnumStrings[0];
                            }

                            aszOutputStrings[iLineIndex++] = "DEFAULT_VALUE AUTO";
                            aszOutputStrings[iLineIndex++] = "/end COMPU_TAB_RANGE";
                        }
                        else if ((1 == msm) && (0 == msb))
                        {
                            aszOutputStrings[iLineIndex++] = "IDENTICAL %" + mszFormat + " " + mszUnits;
                            aszOutputStrings[iLineIndex++] = "/end COMPU_METHOD";
                        }
                        else
                        {
                            aszOutputStrings[iLineIndex++] = "LINEAR %" + mszFormat + " " + mszUnits;
                            aszOutputStrings[iLineIndex++] = "COEFFS_LINEAR " + msm.ToString() + " " + msb.ToString();
                            aszOutputStrings[iLineIndex++] = "/end COMPU_METHOD";
                        }

                        break;
                    }
                case "CURVE":
                    {
                        aszOutputStrings[iLineIndex++] = "/begin " + mszMode + " " + mszName;
                        aszOutputStrings[iLineIndex++] = "\"" + mszHelp + "\"";
                        aszOutputStrings[iLineIndex++] = "CURVE";
                        aszOutputStrings[iLineIndex++] = "0x" + mu32Address.ToString("X8");
                        aszOutputStrings[iLineIndex++] = szDataTypeString;
                        aszOutputStrings[iLineIndex++] = mszCompuMethod;
                        aszOutputStrings[iLineIndex++] = mszGroup;
                        aszOutputStrings[iLineIndex++] = msElementMin.ToString();
                        aszOutputStrings[iLineIndex++] = msElementMax.ToString();

                        if (0 < miXIndexCount)
                        {
                            aszOutputStrings[iLineIndex++] = "\t" + "/begin AXIS_DESCR";
                            aszOutputStrings[iLineIndex++] = "\t" + "STD_AXIS";
                            aszOutputStrings[iLineIndex++] = "\t" + mszXIndexVar;
                            aszOutputStrings[iLineIndex] = "\t" + "CM_" + mszXIndexVar.ToUpper();
                            aszOutputStrings[iLineIndex] = aszOutputStrings[iLineIndex].Replace(' ', '_');
                            iLineIndex++;
                            aszOutputStrings[iLineIndex++] = "\t" + miXIndexCount.ToString();
                            aszOutputStrings[iLineIndex++] = "\t" + "0";
                            aszOutputStrings[iLineIndex++] = "\t" + "0";
                            aszOutputStrings[iLineIndex++] = "\t" + "AXIS_PTS_REF " + mszName + "_XAXIS";
                            aszOutputStrings[iLineIndex++] = "\t" + "/end AXIS_DESCR";
                            aszOutputStrings[iLineIndex++] = "/end" + " " + mszMode;
                            aszOutputStrings[iLineIndex++] = "";
                            aszOutputStrings[iLineIndex++] = "";
                        }
                        if (0 < miYIndexCount)
                        {
                            aszOutputStrings[iLineIndex++] = "\t" + "/begin AXIS_DESCR";
                            aszOutputStrings[iLineIndex++] = "\t" + "STD_AXIS";
                            aszOutputStrings[iLineIndex++] = "\t" + mszYIndexVar;
                            aszOutputStrings[iLineIndex++] = "\t" + "CM_" + mszYIndexVar;
                            aszOutputStrings[iLineIndex++] = "\t" + miYIndexCount.ToString();
                            aszOutputStrings[iLineIndex++] = "\t" + "0";
                            aszOutputStrings[iLineIndex++] = "\t" + "0";
                            aszOutputStrings[iLineIndex++] = "\t" + "AXIS_PTS_REF " + mszName + "_YAXIS";
                            aszOutputStrings[iLineIndex++] = "\t" + "/end AXIS_DESCR";
                            aszOutputStrings[iLineIndex++] = "/end" + " " + mszMode;
                            aszOutputStrings[iLineIndex++] = "";
                            aszOutputStrings[iLineIndex++] = "";
                        }

                        aszOutputStrings[iLineIndex++] = "/begin COMPU_METHOD " + mszCompuMethod;
                        aszOutputStrings[iLineIndex++] = "Conversion for " + "\"" + mszName + "\"";

                        if ((1 == msm) && (0 == msb))
                        {
                            aszOutputStrings[iLineIndex++] = "IDENTICAL %" + mszFormat + " " + mszUnits;
                        }
                        else
                        {
                            aszOutputStrings[iLineIndex++] = "LINEAR %" + mszFormat + " " + mszUnits;
                            aszOutputStrings[iLineIndex++] = "COEFFS_LINEAR " + msm.ToString() + " " + msb.ToString();
                        }
                        aszOutputStrings[iLineIndex++] = "/end COMPU_METHOD";
                        break;
                    }
                    case "MAP":
                    {
                        aszOutputStrings[iLineIndex++] = "/begin " + mszMode + " " + mszName;
                        aszOutputStrings[iLineIndex++] = "\"" + mszHelp + "\"";
                        aszOutputStrings[iLineIndex++] = "MAP";
                        aszOutputStrings[iLineIndex++] = "0x" + mu32Address.ToString("X8");
                        aszOutputStrings[iLineIndex++] = szDataTypeString;
                        aszOutputStrings[iLineIndex++] = mszCompuMethod;
                        aszOutputStrings[iLineIndex++] = mszGroup;
                        aszOutputStrings[iLineIndex++] = msElementMin.ToString();
                        aszOutputStrings[iLineIndex++] = msElementMax.ToString();

                        if (0 < miXIndexCount)
                        {
                            aszOutputStrings[iLineIndex++] = "\t" + "/begin AXIS_DESCR";
                            aszOutputStrings[iLineIndex++] = "\t" + "STD_AXIS";
                            aszOutputStrings[iLineIndex++] = "\t" + mszXIndexVar;
                            aszOutputStrings[iLineIndex] = "\t" + "CM_" + mszXIndexVar.ToUpper();
                            aszOutputStrings[iLineIndex] = aszOutputStrings[iLineIndex].Replace(' ', '_');
                            iLineIndex++;
                            aszOutputStrings[iLineIndex++] = "\t" + miXIndexCount.ToString();
                            aszOutputStrings[iLineIndex++] = "\t" + "0";
                            aszOutputStrings[iLineIndex++] = "\t" + "0";
                            aszOutputStrings[iLineIndex++] = "\t" + "AXIS_PTS_REF " + mszName + "_XAXIS";

                            if (0 == miYIndexCount)
                            {
                                aszOutputStrings[iLineIndex++] = "\t" + "/end AXIS_DESCR";
                                aszOutputStrings[iLineIndex++] = "/end" + " " + mszMode;
                                aszOutputStrings[iLineIndex++] = "";
                                aszOutputStrings[iLineIndex++] = "";
                            }
                            else
                            {
                                aszOutputStrings[iLineIndex++] = "\t" + "/end AXIS_DESCR";
                            }
                        }
                        if (0 < miYIndexCount)
                        {
                            aszOutputStrings[iLineIndex++] = "\t" + "/begin AXIS_DESCR";
                            aszOutputStrings[iLineIndex++] = "\t" + "STD_AXIS";
                            aszOutputStrings[iLineIndex++] = "\t" + mszYIndexVar;
                            aszOutputStrings[iLineIndex] = "\t" + "CM_" + mszYIndexVar.ToUpper();
                            aszOutputStrings[iLineIndex] = aszOutputStrings[iLineIndex].Replace(' ', '_');
                            iLineIndex++;
                            aszOutputStrings[iLineIndex++] = "\t" + miYIndexCount.ToString();
                            aszOutputStrings[iLineIndex++] = "\t" + "0";
                            aszOutputStrings[iLineIndex++] = "\t" + "0";
                            aszOutputStrings[iLineIndex++] = "\t" + "AXIS_PTS_REF " + mszName + "_YAXIS";

                            //if (0 == miYIndexCount)
                            //{
                                aszOutputStrings[iLineIndex++] = "\t" + "/end AXIS_DESCR";
                                aszOutputStrings[iLineIndex++] = "/end" + " " + mszMode;
                                aszOutputStrings[iLineIndex++] = "";
                                aszOutputStrings[iLineIndex++] = "";
                            //}
                            //else
                            //{
                                //aszOutputStrings[iLineIndex++] = "\t" + "/end AXIS_DESCR";
                            //}
                        }

                        aszOutputStrings[iLineIndex++] = "/begin COMPU_METHOD " + mszCompuMethod;
                        aszOutputStrings[iLineIndex++] = "Conversion for " + "\"" + mszName + "\"";

                        if ((1 == msm) && (0 == msb))
                        {
                            aszOutputStrings[iLineIndex++] = "IDENTICAL %" + mszFormat + " " + mszUnits;
                        }
                        else
                        {
                            aszOutputStrings[iLineIndex++] = "LINEAR %" + mszFormat + " " + mszUnits;
                            aszOutputStrings[iLineIndex++] = "COEFFS_LINEAR " + msm.ToString() + " " + msb.ToString();
                        }
                        aszOutputStrings[iLineIndex++] = "/end COMPU_METHOD";
                        break;
                    }
                case "AXIS_PTS":
                    {
                        aszOutputStrings[iLineIndex++] = "/begin " + mszMode + " " + mszName;
                        aszOutputStrings[iLineIndex++] = "\"" + mszHelp + "\"";
                        aszOutputStrings[iLineIndex++] = "0x" + mu32Address.ToString("X8");
                        aszOutputStrings[iLineIndex++] = szDataTypeString;
                        
                        if (0 < miXIndexCount)
                        {
                            aszOutputStrings[iLineIndex++] = mszXIndexVar;
                            aszOutputStrings[iLineIndex] = "CM_" + mszXIndexVar.ToUpper();
                            aszOutputStrings[iLineIndex] = aszOutputStrings[iLineIndex++].Replace(' ', '_');
                        }
                        else
                        {
                            aszOutputStrings[iLineIndex++] = mszYIndexVar;
                            aszOutputStrings[iLineIndex] = "CM_" + mszYIndexVar.ToUpper();
                            aszOutputStrings[iLineIndex] = aszOutputStrings[iLineIndex++].Replace(' ', '_');
                        }                        

                        aszOutputStrings[iLineIndex++] = (0 < miXIndexCount) ?
                            miXIndexCount.ToString() : miYIndexCount.ToString();
                        aszOutputStrings[iLineIndex++] = msElementMin.ToString();
                        aszOutputStrings[iLineIndex++] = msElementMax.ToString();
                        aszOutputStrings[iLineIndex++] = "AXIS_PTS_REF " + mszName;
                        aszOutputStrings[iLineIndex++] = "/end" + " " + mszMode;
                        aszOutputStrings[iLineIndex++] = "";
                        aszOutputStrings[iLineIndex++] = "";
                        break;
                    }
                default:
                    {
                        break;
                    }
            }

            return aszOutputStrings;
        }
    }

    public struct tstTypeDef
    {
        public string szDataType;
        public string szTypeAlias;
    };
}