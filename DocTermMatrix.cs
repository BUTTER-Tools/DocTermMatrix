using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;
using System.Text;
using System.Drawing;
using PluginContracts;
using OutputHelperLib;
using System.Linq;


namespace DocTermMatrix
{
    public class DocTermMatrix : Plugin
    {


        public string[] InputType { get; } = { "Tokens" };
        public string OutputType { get; } = "OutputArray";

        public Dictionary<int, string> OutputHeaderData { get; set; } = new Dictionary<int, string>() { { 0, "TokenCount" } };
        public bool InheritHeader { get; } = false;

        #region Plugin Details and Info

        public string PluginName { get; } = "Doc-Term Matrix";
        public string PluginType { get; } = "Language Analysis";
        public string PluginVersion { get; } = "1.0.1";
        public string PluginAuthor { get; } = "Ryan L. Boyd (ryan@ryanboyd.io)";
        public string PluginDescription { get; } = "This plugin will generate a document-term matrix (DTM) from your texts. This plugin requires that you have " +
                                                   "a frequency list already generated via the \"Frequency List\" plugin so that it knows which words to include " +
                                                   "in your DTM output. You can also choose various weighting methods, including Binary (i.e., \"one-hot\"), relative " +
                                                   "frequencies, and weighted frequencies." + Environment.NewLine + Environment.NewLine +
                                                   "Note that \"collocates\" are prioritized by this plugin, unlike the Frequency List plugin. Consider the example sentence:" + Environment.NewLine + Environment.NewLine +
                                                   "\t\"I am interested in health, and I study health behaviors.\"" + Environment.NewLine + Environment.NewLine +
                                                   "If you generated your Frequency List with 2-grams, you would get a score of 2 for the word \"health\", and a score of 1 for the phrase \"health behaviors\". However " +
                                                   "the Doc-Term Matrix plugin looks for the highest-order n-grams when scanning texts. Therefore, the same example sentence would only get scored as " +
                                                   "1 for the word \"health\", and a score of 1 for the phrase \"health behaviors\".";
        public string PluginTutorial { get; } = "https://youtu.be/lN1m08nbuBg";
        public bool TopLevel { get; } = false;


        public Icon GetPluginIcon
        {
            get
            {
                return Properties.Resources.icon;
            }
        }

        #endregion




        private string CSVFileLocation { get; set; } = "";
        private string SelectedEncoding { get; set; } = "utf-8";
        private string Delimiter { get; set; } = ",";
        private string Quote { get; set; } = "\"";
        private decimal wordInclusionParam { get; set; } = 5;
        private string wordInclusionMethod { get; set; } = "N-grams Appearing in >= X% of Documents:";
        private string weightingMethod { get; set; } = "Binary / One-Hot";
        Dictionary<string, double[]> FreqList { get; set; }
        int maxWCinFreqList { get; set; }

        Dictionary<string, int> OutputArrayMap { get; set; }



        public void ChangeSettings()
        {

            using (var form = new SettingsForm_DocTermMatrix(CSVFileLocation, SelectedEncoding, Delimiter, Quote, wordInclusionMethod, wordInclusionParam, weightingMethod))
            {


                form.Icon = Properties.Resources.icon;
                form.Text = PluginName;


                var result = form.ShowDialog();
                if (result == DialogResult.OK)
                {
                    SelectedEncoding = form.SelectedEncoding;
                    CSVFileLocation = form.CSVFileLocation;
                    Delimiter = form.Delimiter;
                    Quote = form.Quote;
                    wordInclusionParam = form.wordInclusionParam;
                    wordInclusionMethod = form.wordInclusionMethod;
                    weightingMethod = form.weightingMethod;

                }
            }
        }





        public Payload RunPlugin(Payload Input)
        {



            Payload pData = new Payload();
            pData.FileID = Input.FileID;
            pData.SegmentID = Input.SegmentID;




            for (int i = 0; i < Input.StringArrayList.Count; i++)
            {

                //this is the actual string array that we'll write out later
                string[] OutputArray = new string[OutputArrayMap.Count + 1];
                for (int j = 0; j < OutputArray.Length; j++) OutputArray[j] = "";

                double[] OutputScores = new double[OutputArrayMap.Count];
                for (int j = 0; j < OutputScores.Length; j++) OutputScores[j] = 0;

                int TotalStringLength = Input.StringArrayList[i].Length;

                //if there are 
                if (TotalStringLength > 0)
                {
                    for (int j = 0; j < TotalStringLength; j++)
                    {

                        //build the n-gram that we're looking for
                        for (int NumberOfWords = maxWCinFreqList; NumberOfWords > 0; NumberOfWords--)
                        {
                            if (j + NumberOfWords - j >= TotalStringLength) continue;

                            string TargetString;

                            if (NumberOfWords > 1)
                            {
                                TargetString = String.Join(" ", Input.StringArrayList[i].Skip(j).Take(NumberOfWords).ToArray());
                            }
                            else
                            {
                                TargetString = Input.StringArrayList[i][j];
                            }

                            //look for the n-gram
                            if (FreqList.ContainsKey(TargetString))
                            {
                                OutputScores[OutputArrayMap[TargetString]]++;
                                j += NumberOfWords - 1;
                                break;
                            }
                        }
                    }


                    //now that we've counted all of the n-grams, we want to apply the scoring method
                    OutputScores = applyScoringMethod(OutputScores, TotalStringLength);

                }


                OutputArray[0] = TotalStringLength.ToString();
                for (int j = 0; j < OutputArrayMap.Count; j++) OutputArray[j + 1] = OutputScores[j].ToString();

                pData.SegmentNumber.Add(Input.SegmentNumber[i]);
                pData.StringArrayList.Add(OutputArray);


            }

            return (pData);

        }









        public void Initialize()
        {

            FreqList = new Dictionary<string, double[]>();
            OutputHeaderData = new Dictionary<int, string>();
            OutputHeaderData.Add(0, "TokenCount");
            OutputArrayMap = new Dictionary<string, int>();
            maxWCinFreqList = 0;

            try
            {
                using (var stream = File.OpenRead(CSVFileLocation))
                using (var reader = new StreamReader(stream, encoding: Encoding.GetEncoding(SelectedEncoding)))
                {
                    var data = CsvParser.ParseHeadAndTail(reader, Delimiter[0], Quote[0]);

                    var header = data.Item1;
                    var lines = data.Item2;

                    string[] HeadersFromFile = header.ToArray<string>();

                    foreach (var line in lines)
                    {
                        //read in each row of the frequency list
                        //0 - TextID
                        //1 - Segment
                        //2 - SegmentID
                        //3 - Frequency
                        //4 - Documents (we won't keep this one)
                        //5 - ObsPct
                        //6 - IDF
                        //7 - PhraseLength
                        //8 - Pointwise Mutual Information

                        double[] rowdat = new double[3] { 0, 0, 0 };
                        string word = line[2]; //ngram
                        rowdat[0] = Double.Parse(line[3]); //freq
                        rowdat[1] = Double.Parse(line[5]); //obspct
                        rowdat[2] = Double.Parse(line[6]); // idf
                        //rowdat indices:
                        //0 - Frequency
                        //1 - ObsPct
                        //2 - IDF

                        if (!FreqList.ContainsKey(word)) FreqList.Add(word, rowdat);


                    }

                    //drop all items in FreqList that don't meet our criteria
                    FreqList = SubsetFreqList(FreqList);

                    //make sure that we know what the "n" in n-grams is
                    foreach (string word in FreqList.Keys)
                    {
                        if (word.Split().Length > maxWCinFreqList) maxWCinFreqList = word.Split().Length;
                    }

                }


                //now that we've populated the frequency list into a dictionary, we want to drop out the items that aren't going to make the cut

                int outputArrTrack = 0;
                foreach (string key in FreqList.Keys)
                {
                    OutputHeaderData.Add(outputArrTrack + 1, key);
                    OutputArrayMap.Add(key, outputArrTrack);
                    outputArrTrack++;
                }



            }
            catch
            {
                FreqList = new Dictionary<string, double[]>();
                OutputHeaderData = new Dictionary<int, string>();
                OutputHeaderData.Add(0, "TokenCount");
                OutputArrayMap = new Dictionary<string, int>();
                MessageBox.Show("There was an error while trying to read your BUTTER Frequency List file. If you currently have the Frequency List file open in another program, please close it and try again. This error can also be caused when your spreadsheet is not correctly formatted, or that your selections for delimiters and quotes are not the same as what is used in your spreadsheet.", "Error reading spreadsheet", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }








        public bool InspectSettings()
        {
            if (string.IsNullOrEmpty(CSVFileLocation))
            {
                return false;
            }
            else
            {
                return true;
            }
        }





        public Payload FinishUp(Payload Input)
        {
            FreqList.Clear();
            OutputArrayMap.Clear();
            return (Input);
        }


        #region Import/Export Settings
        public void ImportSettings(Dictionary<string, string> SettingsDict)
        {
            CSVFileLocation = SettingsDict["CSVFileLocation"];
            SelectedEncoding = SettingsDict["SelectedEncoding"];
            Delimiter = SettingsDict["Delimiter"];
            Quote = SettingsDict["Quote"];
            wordInclusionParam = Decimal.Parse(SettingsDict["wordInclusionParam"]);
            wordInclusionMethod = SettingsDict["wordInclusionMethod"];
            weightingMethod = SettingsDict["weightingMethod"];
        }



        public Dictionary<string, string> ExportSettings(bool suppressWarnings)
        {
            Dictionary<string, string> SettingsDict = new Dictionary<string, string>();

            SettingsDict.Add("CSVFileLocation", CSVFileLocation);
            SettingsDict.Add("SelectedEncoding", SelectedEncoding);
            SettingsDict.Add("Delimiter", Delimiter);
            SettingsDict.Add("Quote", Quote);
            SettingsDict.Add("wordInclusionParam", wordInclusionParam.ToString());
            SettingsDict.Add("wordInclusionMethod", wordInclusionMethod);
            SettingsDict.Add("weightingMethod", weightingMethod);


            return (SettingsDict);
        }
        #endregion





        private Dictionary<string, double[]> SubsetFreqList(Dictionary<string, double[]> FL)
        {

            //rowdat indices:
            //0 - Frequency
            //1 - ObsPct
            //2 - IDF
            int paramAsInt = (int)Math.Round(wordInclusionParam);

            string[] keyArray = FL.Keys.ToArray();

            switch (wordInclusionMethod)
            {
                case "N-grams Appearing in >= X% of Documents:":
                    foreach (string key in keyArray)
                    {
                        if (FL[key][1] < (double)wordInclusionParam) FL.Remove(key);
                    }
                    break;

                case "N-grams with Raw Frequency >= X:":
                    foreach (string key in keyArray)
                    {
                        if (FL[key][0] < (double)wordInclusionParam) FL.Remove(key);
                    }
                    break;

                case "X most Frequent N-Grams (by % of Documents):":
                    if (FL.Count() > paramAsInt)
                    {
                        double[] sortArr = new double[FL.Count()];
                        int indexTrack = 0;
                        foreach (string key in keyArray)
                        {
                            sortArr[indexTrack] = FL[key][1];
                            indexTrack++;
                        }

                        Array.Sort(sortArr);
                        Array.Reverse(sortArr);

                        double cutOffThreshold = sortArr[paramAsInt - 1];

                        foreach (string key in keyArray)
                        {
                            if (FL[key][1] < cutOffThreshold) FL.Remove(key);
                        }

                    }
                    break;

                case "X most Frequent N-Grams (by Raw Frequency):":
                    if (FL.Count() > paramAsInt)
                    {
                        double[] sortArr = new double[FL.Count()];
                        int indexTrack = 0;
                        foreach (string key in keyArray)
                        {
                            sortArr[indexTrack] = FL[key][0];
                            indexTrack++;
                        }

                        Array.Sort(sortArr);
                        Array.Reverse(sortArr);

                        double cutOffThreshold = sortArr[paramAsInt - 1];

                        foreach (string key in keyArray)
                        {
                            if (FL[key][0] < cutOffThreshold) FL.Remove(key);
                        }

                    }
                    break;
            }

            return FL;
        }








        private double[] applyScoringMethod(double[] scores, int tokenCount)
        {

            switch (weightingMethod)
            {
                case "Binary / One-Hot":
                    for (int i = 0; i < scores.Length; i++)
                    {
                        if (scores[i] > 0) scores[i] = 1;
                    }
                    break;

                case "Raw Frequency":
                    break;

                case "Relative Frequency":
                    for (int i = 0; i < scores.Length; i++)
                    {
                        scores[i] = scores[i] / tokenCount;
                    }
                    break;

                case "TF-IDF":
                    for (int i = 0; i < scores.Length; i++)
                    {
                        //seems a little convoluted (and it is), but this structure makes sense
                        // we look at OutputHeaderData[i+1] to get the actual string/n-gram corresponding to the score
                        //then we go back to the FreqList dictionary to get the associated array
                        //and FreqList[key][2] is the IDF weight
                        scores[i] = scores[i] * FreqList[OutputHeaderData[i+1]][2];
                    }
                    break;
            }

            return scores;
        }
    }

}