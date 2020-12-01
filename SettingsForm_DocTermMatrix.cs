using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Linq;
using System.Collections.Generic;





namespace DocTermMatrix
{
    internal partial class SettingsForm_DocTermMatrix : Form
    {


        #region Get and Set Options

        public string CSVFileLocation { get; set; }
        public string SelectedEncoding { get; set; }
        public string Delimiter { get; set; }
        public string Quote { get; set; }
        public decimal wordInclusionParam { get; set; }
        public string wordInclusionMethod { get; set; }
        public string weightingMethod { get; set; }

        #endregion



        public SettingsForm_DocTermMatrix(string CSVFileLocation, string SelectedEncoding, string Delimiter, string Quote,
                                         string wordInclusionMethodIncoming, decimal wordInclusionParamIncoming, string weightingMethodIncoming)
        {
            InitializeComponent();

            foreach (var encoding in Encoding.GetEncodings())
            {
                EncodingDropdown.Items.Add(encoding.Name);
            }

            try
            {
                EncodingDropdown.SelectedIndex = EncodingDropdown.FindStringExact(SelectedEncoding);
            }
            catch
            {
                EncodingDropdown.SelectedIndex = EncodingDropdown.FindStringExact(Encoding.Default.BodyName);
            }


            wordInclusionMethodDropdown.Items.Add("N-grams Appearing in >= X% of Documents:");
            wordInclusionMethodDropdown.Items.Add("N-grams with Raw Frequency >= X:");
            wordInclusionMethodDropdown.Items.Add("X most Frequent N-Grams (by % of Documents):");
            wordInclusionMethodDropdown.Items.Add("X most Frequent N-Grams (by Raw Frequency):");
            wordInclusionMethodDropdown.SelectedIndex = wordInclusionMethodDropdown.FindStringExact(wordInclusionMethodIncoming);

            weightingMethodDropdown.Items.Add("Binary / One-Hot");
            weightingMethodDropdown.Items.Add("Raw Frequency");
            weightingMethodDropdown.Items.Add("Relative Frequency");
            weightingMethodDropdown.Items.Add("TF-IDF");
            weightingMethodDropdown.SelectedIndex = weightingMethodDropdown.FindStringExact(weightingMethodIncoming);

            CSVDelimiterTextbox.Text = Delimiter;
            CSVQuoteTextbox.Text = Quote;
            SelectedFileTextbox.Text = CSVFileLocation;
            wordInclParamNumericBox.Value = wordInclusionParamIncoming;

            this.SelectedEncoding = SelectedEncoding;

           
        }












        private void SetFolderButton_Click(object sender, System.EventArgs e)
        {


            SelectedFileTextbox.Text = "";

            if (CSVDelimiterTextbox.TextLength < 1 || CSVQuoteTextbox.TextLength < 1)
            {
                MessageBox.Show("You must enter characters for your delimiter and quotes, respectively. This plugin does not know how to read a delimited spreadsheet without this information.", "I need details for your spreadsheet!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }




            using (var dialog = new OpenFileDialog())
            {

                dialog.Multiselect = false;
                dialog.CheckFileExists = true;
                dialog.CheckPathExists = true;
                dialog.ValidateNames = true;
                dialog.Title = "Please choose the BUTTER Frequency file that you would like to read";
                dialog.FileName = "BUTTER-FrequencyList.csv";
                dialog.Filter = "Comma-Separated Values (CSV) File (*.csv)|*.csv";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    SelectedFileTextbox.Text = dialog.FileName;

                    
                    //try
                    //{
                    //    using (var stream = File.OpenRead(dialog.FileName))
                    //    using (var reader = new StreamReader(stream, encoding: Encoding.GetEncoding(SelectedEncoding)))
                    //    {
                    //        var data = CsvParser.ParseHeadAndTail(reader, CSVDelimiterTextbox.Text[0], CSVQuoteTextbox.Text[0]);

                    //        var header = data.Item1;
                    //        var lines = data.Item2;

                    //        string[] HeadersFromFile = header.ToArray<string>();

                    //    }

                    //}
                    //catch
                    //{
                    //    MessageBox.Show("There was an error while trying to read your BUTTER Frequency List file. If you currently have the Frequency List file open in another program, please close it and try again. This error can also be caused when your spreadsheet is not correctly formatted, or that your selections for delimiters and quotes are not the same as what is used in your spreadsheet.", "Error reading spreadsheet", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    //    return;
                    //}


                }
                else
                {
                    SelectedFileTextbox.Text = "";
                }
            }
        }
















        private void OKButton_Click(object sender, System.EventArgs e)
        {
            this.SelectedEncoding = EncodingDropdown.SelectedItem.ToString();
            this.CSVFileLocation = SelectedFileTextbox.Text;
            this.weightingMethod = weightingMethodDropdown.SelectedItem.ToString();
            this.wordInclusionMethod = wordInclusionMethodDropdown.SelectedItem.ToString();
            this.wordInclusionParam = wordInclParamNumericBox.Value;
           

            if (CSVQuoteTextbox.Text.Length > 0)
            {
                this.Quote = CSVQuoteTextbox.Text;
            }
            else
            {
                this.Quote = "\"";
            }
            if (CSVDelimiterTextbox.Text.Length > 0)
            {
                this.Delimiter = CSVDelimiterTextbox.Text;
            }
            else
            {
                this.Delimiter = ",";
            }
            



            this.DialogResult = DialogResult.OK;

        }
    }
}
