using SpeechAndTTS;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources.Core;
using Windows.Foundation;
using Windows.Globalization;
using Windows.Graphics.Display;
using Windows.Media.MediaProperties;
using Windows.Media.SpeechRecognition;
using Windows.Media.SpeechSynthesis;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;

//“空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 上有介绍

namespace SpeechIt.Views
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page //, INotifyPropertyChanged
    {
        private static int sizeMinW = 640;
        private static int sizeMinH = 400;
        private Size minSize = new Size(sizeMinW, sizeMinH);

        internal static float m_DPI = DisplayInformation.GetForCurrentView().LogicalDpi;
        internal float ConvertPixelsToDips(int pixels)
        {
            return (pixels * 96f / m_DPI);
        }
        internal float ConvertPixelsToDips(float pixels)
        {
            return (pixels * 96f / m_DPI);
        }
        internal float ConvertPixelsToDips(double pixels)
        {
            return ((float)pixels * 96f / m_DPI);
        }

        private SpeechSynthesizer synth;
        private SpeechRecognizer recognizer;

        private static uint HResultRecognizerNotFound = 0x8004503a;

        private ResourceContext speechContext;
        private ResourceMap speechResourceMap;
        private bool isPopulatingLanguages = false;
        private bool isListening = false;

        private StorageFile InFile = null, OutFile = null;
        private MediaTranscoder trans = null;
        private CancellationTokenSource canceltsrc = null;

        //public event PropertyChangedEventHandler PropertyChanged;

        //private void Set<T>(ref T storage, T value, [CallerMemberName]string propertyName = null)
        //{
        //    if (Equals(storage, value))
        //    {
        //        return;
        //    }

        //    storage = value;
        //    OnPropertyChanged(propertyName);
        //}

        //private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// Look up the supported languages for this speech recognition scenario, 
        /// that are installed on this machine, and populate a dropdown with a list.
        /// </summary>
        private void PopulateLanguageDropdown()
        {
            // disable the callback so we don't accidentally trigger initialization of the recognizer
            // while initialization is already in progress.
            isPopulatingLanguages = true;

            Language defaultLanguage = SpeechRecognizer.SystemSpeechLanguage;
            IEnumerable<Language> supportedLanguages = SpeechRecognizer.SupportedGrammarLanguages;
            foreach (Language lang in supportedLanguages)
            {
                ComboBoxItem item = new ComboBoxItem
                {
                    Tag = lang,
                    Content = lang.DisplayName
                };

                cbLanguageSelection.Items.Add(item);
                if (lang.LanguageTag == defaultLanguage.LanguageTag)
                {
                    item.IsSelected = true;
                    cbLanguageSelection.SelectedItem = item;
                }
            }
            isPopulatingLanguages = false;
        }

        /// <summary>
        /// Initialize Speech Recognizer and compile constraints.
        /// </summary>
        /// <param name="recognizerLanguage">Language to use for the speech recognizer</param>
        /// <returns>Awaitable task.</returns>
        private async Task InitializeRecognizer(Language recognizerLanguage)
        {
            if (recognizer != null)
            {
                // cleanup prior to re-initializing this scenario.
                recognizer.StateChanged -= SpeechRecognizer_StateChanged;
                recognizer.ContinuousRecognitionSession.Completed -= ContinuousRecognitionSession_Completed;
                recognizer.ContinuousRecognitionSession.ResultGenerated -= ContinuousRecognitionSession_ResultGenerated;

                this.recognizer.Dispose();
                this.recognizer = null;
            }

            try
            {
                this.recognizer = new SpeechRecognizer(recognizerLanguage);

                // Provide feedback to the user about the state of the recognizer. This can be used to provide visual feedback in the form
                // of an audio indicator to help the user understand whether they're being heard.
                recognizer.StateChanged += SpeechRecognizer_StateChanged;

                //// Build a command-list grammar. Commands should ideally be drawn from a resource file for localization, and 
                //// be grouped into tags for alternate forms of the same command.
                //recognizer.Constraints.Add(
                //    new SpeechRecognitionListConstraint(
                //        new List<string>()
                //        {
                //        speechResourceMap.GetValue("ListGrammarGoHome", speechContext).ValueAsString
                //        }, "Home"));
                //recognizer.Constraints.Add(
                //    new SpeechRecognitionListConstraint(
                //        new List<string>()
                //        {
                //        speechResourceMap.GetValue("ListGrammarGoToContosoStudio", speechContext).ValueAsString
                //        }, "GoToContosoStudio"));
                //recognizer.Constraints.Add(
                //    new SpeechRecognitionListConstraint(
                //        new List<string>()
                //        {
                //        speechResourceMap.GetValue("ListGrammarShowMessage", speechContext).ValueAsString,
                //        speechResourceMap.GetValue("ListGrammarOpenMessage", speechContext).ValueAsString
                //        }, "Message"));
                //recognizer.Constraints.Add(
                //    new SpeechRecognitionListConstraint(
                //        new List<string>()
                //        {
                //        speechResourceMap.GetValue("ListGrammarSendEmail", speechContext).ValueAsString,
                //        speechResourceMap.GetValue("ListGrammarCreateEmail", speechContext).ValueAsString
                //        }, "Email"));
                //recognizer.Constraints.Add(
                //    new SpeechRecognitionListConstraint(
                //        new List<string>()
                //        {
                //        speechResourceMap.GetValue("ListGrammarCallNitaFarley", speechContext).ValueAsString,
                //        speechResourceMap.GetValue("ListGrammarCallNita", speechContext).ValueAsString
                //        }, "CallNita"));
                //recognizer.Constraints.Add(
                //    new SpeechRecognitionListConstraint(
                //        new List<string>()
                //        {
                //        speechResourceMap.GetValue("ListGrammarCallWayneSigmon", speechContext).ValueAsString,
                //        speechResourceMap.GetValue("ListGrammarCallWayne", speechContext).ValueAsString
                //        }, "CallWayne"));

                //// Update the help text in the UI to show localized examples
                //string uiOptionsText = string.Format("Try saying '{0}', '{1}' or '{2}'",
                //    speechResourceMap.GetValue("ListGrammarGoHome", speechContext).ValueAsString,
                //    speechResourceMap.GetValue("ListGrammarGoToContosoStudio", speechContext).ValueAsString,
                //    speechResourceMap.GetValue("ListGrammarShowMessage", speechContext).ValueAsString);
                ////listGrammarHelpText.Text = string.Format("{0}\n{1}",
                ////    speechResourceMap.GetValue("ListGrammarHelpText", speechContext).ValueAsString,
                ////    uiOptionsText);

                SpeechRecognitionCompilationResult result = await recognizer.CompileConstraintsAsync();
                if (result.Status != SpeechRecognitionResultStatus.Success)
                {
                    // Disable the recognition buttons.
                    btnListen.IsEnabled = false;

                    // Let the user know that the grammar didn't compile properly.
                    edHearState.Text = AppResources.GetString("UnableCompileGrammar");
                }
                else
                {
                    btnListen.IsEnabled = true;

                    // Handle continuous recognition events. Completed fires when various error states occur. ResultGenerated fires when
                    // some recognized phrases occur, or the garbage rule is hit.
                    recognizer.ContinuousRecognitionSession.Completed += ContinuousRecognitionSession_Completed;
                    recognizer.ContinuousRecognitionSession.ResultGenerated += ContinuousRecognitionSession_ResultGenerated;
                }
            }
            catch (Exception ex)
            {
                if ((uint)ex.HResult == HResultRecognizerNotFound)
                {
                    btnListen.IsEnabled = false;

                    edHearState.Visibility = Visibility.Visible;
                    edHearState.Text = AppResources.GetString("SpeechLanguageNotInstalled");
                }
                else
                {
                    var messageDialog = new Windows.UI.Popups.MessageDialog(ex.Message, AppResources.GetString("Exception"));
                    await messageDialog.ShowAsync();
                }
            }
        }

        /// <summary>
        /// Handle events fired when error conditions occur, such as the microphone becoming unavailable, or if
        /// some transient issues occur.
        /// </summary>
        /// <param name="sender">The continuous recognition session</param>
        /// <param name="args">The state of the recognizer</param>
        private async void ContinuousRecognitionSession_Completed(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionCompletedEventArgs args)
        {
            if (args.Status != SpeechRecognitionResultStatus.Success)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    edHearState.Text = $"{AppResources.GetString("RecognitionCompleted")} {AppResources.GetString(args.Status.ToString())}";
                    BtnCancel.Visibility = Visibility.Collapsed;
                    btnListen.Content = AppResources.GetString("Listen");
                    btnListen.IsChecked = false;
                    cbLanguageSelection.IsEnabled = true;
                    isListening = false;
                });
            }
        }

        /// <summary>
        /// Handle events fired when a result is generated. This may include a garbage rule that fires when general room noise
        /// or side-talk is captured (this will have a confidence of Rejected typically, but may occasionally match a rule with
        /// low confidence).
        /// </summary>
        /// <param name="sender">The Recognition session that generated this result</param>
        /// <param name="args">Details about the recognized speech</param>
        private async void ContinuousRecognitionSession_ResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            // The garbage rule will not have a tag associated with it, the other rules will return a string matching the tag provided
            // when generating the grammar.
            string tag = AppResources.GetString("unknown");
            if (args.Result.Constraint != null)
            {
                tag = args.Result.Constraint.Tag;
            }
            // Developers may decide to use per-phrase confidence levels in order to tune the behavior of their 
            // grammar based on testing.
            if (args.Result.Confidence == SpeechRecognitionConfidence.Medium ||
                args.Result.Confidence == SpeechRecognitionConfidence.High)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    edHearState.Text = $"{AppResources.GetString("Heard")}: {args.Result.Text}, ({AppResources.GetString("Tag")}: {tag}, {AppResources.GetString("Confidence")}: {args.Result.Confidence.ToString()})";
                    edContent.Text += string.Format("{0}", args.Result.Text);
                    edContent.SelectionStart = edContent.Text.Length;
                });
            }
            else
            {
                // In some scenarios, a developer may choose to ignore giving the user feedback in this case, if speech
                // is not the primary input mechanism for the application.
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    edHearState.Text = $"{AppResources.GetString("VoiceCatchFailed")}. ({AppResources.GetString("Heard")}: {args.Result.Text}, {AppResources.GetString("Tag")}: {tag}, {AppResources.GetString("Confidence")}: {args.Result.Confidence.ToString()})";
                });
            }
        }

        /// <summary>
        /// Provide feedback to the user based on whether the recognizer is receiving their voice input.
        /// </summary>
        /// <param name="sender">The recognizer that is currently running.</param>
        /// <param name="args">The current state of the recognizer.</param>
        private async void SpeechRecognizer_StateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var state = args.State.ToString().Trim();
                edHearState.Text = AppResources.GetString(state);
                //rootPage.NotifyUser(args.State.ToString(), NotifyType.StatusMessage);
                if (state.Equals("Idle", StringComparison.CurrentCultureIgnoreCase))
                {
                    btnListen.Content = AppResources.GetString("Listen");
                    btnListen.IsChecked = false;
                    cbLanguageSelection.IsEnabled = true;
                    isListening = false;
                }
            });
        }

        /// <summary>
        /// Stream to Byte
        /// </summary>
        /// <param name="streamin"></param>
        /// <returns></returns>
        private byte[] ReadStream(SpeechSynthesisStream streamin)
        {
            return (ReadStream(streamin.AsStream()));
        }

        /// <summary>
        /// Stream to Byte
        /// </summary>
        /// <param name="streamin"></param>
        /// <returns></returns>
        private byte[] ReadStream(Stream streamin)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = streamin.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        private async Task<string> InputBox(string caption)
        {
            TextBox edInput = new TextBox();
            edInput.AcceptsReturn = false;
            edInput.Height = 32;
            ContentDialog dialog = new ContentDialog();
            dialog.Content = edInput;
            dialog.Title = caption;
            dialog.IsSecondaryButtonEnabled = true;
            dialog.PrimaryButtonText = AppResources.GetString("OK");
            dialog.SecondaryButtonText = AppResources.GetString("Cancel");
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                return (edInput.Text);
            else
                return (string.Empty);
        }

        private async Task ProgressRingBox(string caption)
        {
            ProgressRing pr = new ProgressRing();
            pr.Width = 64;
            pr.Height = 64;
            pr.HorizontalAlignment = HorizontalAlignment.Center;
            pr.VerticalAlignment = VerticalAlignment.Center;
            ContentDialog dialog = new ContentDialog();
            dialog.Content = pr;
            dialog.Title = caption;
            await dialog.ShowAsync();
        }

        private async Task Text2Audio(string text, int split = 0)
        {
            // Generate the audio stream from plain text.
            if (edContent.Text.Length <= 0) return;
            string contents = string.Empty;
            if (edContent.SelectionLength > 0) contents = edContent.SelectedText;
            else if (edContent.SelectionStart >= edContent.Text.Length) contents = edContent.Text;
            else contents = edContent.Text.Substring(edContent.SelectionStart);

            if (split > 0)
            {
                string[] splitChar = new string[] { ".", "。", "?", "？", "!", "！" };

                FolderPicker fp = new FolderPicker();
                //fp.SuggestedStartLocation = PickerLocationId.Desktop;
                fp.FileTypeFilter.Add("*");
                var TargetFolder = await fp.PickSingleFolderAsync();
                if (TargetFolder != null)
                {
                    StorageApplicationPermissions.MostRecentlyUsedList.Add(TargetFolder, TargetFolder.Name);
                    if (StorageApplicationPermissions.FutureAccessList.Entries.Count >= 1000)
                        StorageApplicationPermissions.FutureAccessList.Remove(StorageApplicationPermissions.FutureAccessList.Entries.Last().Token);
                    StorageApplicationPermissions.FutureAccessList.Add(TargetFolder, TargetFolder.Name);

                    var ffn = await InputBox(AppResources.GetString("InputFileName"));
                    if (string.IsNullOrEmpty(ffn)) return;

                    //await ProgressRingBox(AppResources.GetString("Waiting"));
                    ProgressRing.Visibility = Visibility.Visible;
                    ProgressRing.IsActive = true;

                    var fn = Path.GetFileNameWithoutExtension(ffn);
                    var ext = Path.GetExtension(ffn);
                    if (string.IsNullOrEmpty(ext)) ext = ".mp3";

                    List<string> paras = new List<string>();
                    string[] lines = text.Split(splitChar, StringSplitOptions.RemoveEmptyEntries);
                    StringBuilder sb = new StringBuilder();
                    foreach (string t in lines)
                    {
                        sb.AppendLine(t);
                        if (sb.Length >= split)
                        {
                            paras.Add(sb.ToString());
                            sb.Clear();
                        }
                    }

                    int count = 0;
                    foreach (var line in paras)
                    {
                        var suffix = $"{count}";
                        if (paras.Count > 10000) suffix = $"{count:d05}";
                        else if (paras.Count >= 1000) suffix = $"{count:d04}";
                        else if (paras.Count >= 100) suffix = $"{count:d03}";
                        else if (paras.Count >= 10) suffix = $"{count:d02}";

                        StorageFile fo = await TargetFolder.CreateFileAsync($"{fn}_{suffix}{ext}", CreationCollisionOption.ReplaceExisting);
                        await Text2Audio(line, fo);
                        count++;
                    }
                }
            }
            else
            {
                FileSavePicker fsp = new FileSavePicker();
                fsp.DefaultFileExtension = ".mp3";
                fsp.FileTypeChoices.Add("MP3 file", new List<string>() { ".mp3" });
                fsp.FileTypeChoices.Add("AAC/M4A file", new List<string>() { ".aac", ".m4a" });
                fsp.FileTypeChoices.Add("FLAC file", new List<string>() { ".flac" });
                fsp.FileTypeChoices.Add("ALAC file", new List<string>() { ".alac" });
                fsp.FileTypeChoices.Add("WAV file", new List<string>() { ".wav" });
                fsp.FileTypeChoices.Add("MP4 file", new List<string>() { ".mp4" });
                fsp.SuggestedFileName = "untitled";

                OutFile = await fsp.PickSaveFileAsync();
                ProgressRing.Visibility = Visibility.Visible;
                ProgressRing.IsActive = true;
                await Text2Audio(contents, OutFile);
            }
            ProgressRing.IsActive = false;
            ProgressRing.Visibility = Visibility.Collapsed;
        }

        private async Task Text2Audio(string text, StorageFile audio)
        {
            if (audio != null && !string.IsNullOrEmpty(text))
            {
                if (synth == null) synth = new SpeechSynthesizer();
                var voice = SpeechSynthesizer.AllVoices.Where(o => o.DisplayName == (string)cbVoice.SelectedItem);
                synth.Voice = voice.First();
                synth.Options.AudioPitch = sliderPitch.Value;
                synth.Options.AudioVolume = sliderVolume.Value / 100.0;
                synth.Options.SpeakingRate = sliderSpeed.Value;
                //var options = new SpeechSynthesizerOptions();

                SpeechSynthesisStream stream = await synth.SynthesizeTextToStreamAsync(text);

                trans = new MediaTranscoder()
                {
                    HardwareAccelerationEnabled = true,
                    AlwaysReencode = true,
                    //VideoProcessingAlgorithm = MediaVideoProcessingAlgorithm.MrfCrf444;
                };

                MediaEncodingProfile profile = MediaEncodingProfile.CreateMp3(AudioEncodingQuality.Medium);
                if (audio.ContentType.EndsWith("wav", StringComparison.CurrentCultureIgnoreCase))
                    profile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.Medium);
                else if (audio.ContentType.EndsWith("aac", StringComparison.CurrentCultureIgnoreCase))
                    profile = MediaEncodingProfile.CreateM4a(AudioEncodingQuality.Medium);
                else if (audio.ContentType.EndsWith("m4a", StringComparison.CurrentCultureIgnoreCase))
                    profile = MediaEncodingProfile.CreateM4a(AudioEncodingQuality.Medium);
                else if (audio.FileType.EndsWith("alac", StringComparison.CurrentCultureIgnoreCase))
                    profile = MediaEncodingProfile.CreateAlac(AudioEncodingQuality.Medium);
                else if (audio.ContentType.EndsWith("flac", StringComparison.CurrentCultureIgnoreCase))
                    profile = MediaEncodingProfile.CreateFlac(AudioEncodingQuality.Medium);
                else if (audio.ContentType.EndsWith("mp4", StringComparison.CurrentCultureIgnoreCase))
                    profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Pal);

                using (IRandomAccessStream fso = await audio.OpenAsync(FileAccessMode.ReadWrite))
                {
                    //var trans_result = await trans.PrepareFileTranscodeAsync(InFile, OutFile, profile_mp3);
                    var trans_result = await trans.PrepareStreamTranscodeAsync(stream, fso, profile);
                    if (trans_result.CanTranscode)
                    {
                        if (canceltsrc != null)
                        {
                            canceltsrc.Dispose();
                            canceltsrc = null;
                        }
                        canceltsrc = new CancellationTokenSource();
                        var progress = new Progress<double>(ps =>
                        {
                            edHearState.Text = $"{AppResources.GetString("ProcessingState")} {ps:N0}%";
                            //edHearState.Text = AppResources.GetString("ProcessingState");
                        });
                        await trans_result.TranscodeAsync().AsTask(canceltsrc.Token, progress);
                        edHearState.Text = AppResources.GetString("ProcessFinished");
                    }
                    else
                    {
                        edHearState.Text = AppResources.GetString("CanNotTrans");
                    }
                }
            }
        }

        private void PerformClick(Button button)
        {
            var ap = new ButtonAutomationPeer(button);
            var ip = ap.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            ip?.Invoke();
        }

        public MainPage()
        {
            this.InitializeComponent();
        }

        private void Page_Loading(FrameworkElement sender, object args)
        {
            //ApplicationView.GetForCurrentView().Title = AppResources.AppName;
            minSize = new Size(ConvertPixelsToDips(sizeMinW), ConvertPixelsToDips(sizeMinH));
            ApplicationView.GetForCurrentView().SetPreferredMinSize(minSize);
            ApplicationView.PreferredLaunchViewSize = minSize;
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            if (isListening)
            {
                var ap = new ToggleButtonAutomationPeer(btnListen);
                var ip = ap.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                ip?.Invoke();
            }
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if ((MinWidth > 0 && sizeMinW > e.NewSize.Width) ||
                (MinHeight > 0 && sizeMinH > e.NewSize.Height))
            {
                ApplicationView.GetForCurrentView().TryResizeView(minSize);
            }
            //if ((MinWidth > 0 &&  > e.NewSize.Width) ||
            //    (MinHeight > 0 && sizeMinH > e.NewSize.Height))
            //{
            //    ApplicationView.GetForCurrentView().TryResizeView(e.PreviousSize);
            //}
        }

        private async void Main_LoadedAsync(object sender, RoutedEventArgs e)
        {
            cbVoice.Items.Clear();
            foreach (var voice in SpeechSynthesizer.AllVoices)
            {
                cbVoice.Items.Add(voice.DisplayName);
                if (voice.DisplayName == SpeechSynthesizer.DefaultVoice.DisplayName) cbVoice.SelectedItem = cbVoice.Items.Last();
            }
            isListening = false;

            // Prompt the user for permission to access the microphone. This request will only happen
            // once, it will not re-prompt if the user rejects the permission.
            bool permissionGained = await AudioCapturePermissions.RequestMicrophonePermission();
            if (permissionGained)
            {
                btnListen.IsEnabled = true;

                // Initialize resource map to retrieve localized speech strings.
                Language speechLanguage = SpeechRecognizer.SystemSpeechLanguage;
                string langTag = speechLanguage.LanguageTag;
                speechContext = ResourceContext.GetForCurrentView();
                speechContext.Languages = new string[] { langTag };

                speechResourceMap = ResourceManager.Current.MainResourceMap.GetSubtree("LocalizationSpeechResources");

                PopulateLanguageDropdown();
                await InitializeRecognizer(SpeechRecognizer.SystemSpeechLanguage);
            }
            else
            {
                btnListen.IsEnabled = false;
            }
        }

        private void sliderVolume_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (synth == null) return;
            synth.Options.AudioVolume = e.NewValue;
        }

        private void sliderSpeed_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (synth == null) return;
            synth.Options.SpeakingRate = e.NewValue;
        }

        private void sliderPitch_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (synth == null) return;
            synth.Options.AudioPitch = e.NewValue;
        }

        private void media_MediaOpened(object sender, RoutedEventArgs e)
        {
            btnSpeak.IsChecked = true;
            BtnCancel.Visibility = Visibility.Visible;
        }

        private void media_MediaEnded(object sender, RoutedEventArgs e)
        {
            btnSpeak.IsChecked = false;
            BtnCancel.Visibility = Visibility.Collapsed;
        }

        private void media_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            btnSpeak.IsChecked = false;
            BtnCancel.Visibility = Visibility.Collapsed;
        }

        private void edSplit_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (edSplit.Text.Length <= 0) edSplit.Text = "0";

            var textbox = (TextBox)sender;
            //if (!Regex.IsMatch(textbox.Text, "^\\d*\\.?\\d*$") && textbox.Text != "")
            if (!Regex.IsMatch(textbox.Text, "^\\d+$"))
            {
                int pos = textbox.SelectionStart - 1;
                textbox.Text = textbox.Text.Remove(pos, 1);
                textbox.SelectionStart = pos;
            }
        }

        private async void cbLanguageSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isPopulatingLanguages) return;
            Language targetLang = (Language)(cbLanguageSelection.SelectedItem as ComboBoxItem).Tag;
            await InitializeRecognizer(targetLang);
            edHearState.Text = AppResources.GetString("Idle");
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (media != null) media.Stop();
            if (synth != null) synth = null;
            if (canceltsrc != null)
            {
                canceltsrc.Cancel();
                edHearState.Text = AppResources.GetString("Canceled");
            }
            btnSpeak.IsChecked = false;
            btnListen.IsChecked = false;
            BtnCancel.Visibility = Visibility.Collapsed;
        }

        private async void BtnSpeak_Click(object sender, RoutedEventArgs e)
        {
            //PerformClick(BtnCancel);
            //BtnCancel.Visibility = Visibility.Visible;
            //btnSpeak.IsChecked = true;
            //btnListen.IsChecked = false;

            // Generate the audio stream from plain text.
            if (edContent.Text.Length <= 0) return;
            string contents = string.Empty;
            if (edContent.SelectionLength > 0) contents = edContent.SelectedText;
            else if (edContent.SelectionStart >= edContent.Text.Length) contents = edContent.Text;
            else contents = edContent.Text.Substring(edContent.SelectionStart);

            if (synth == null) synth = new SpeechSynthesizer();
            var voice = SpeechSynthesizer.AllVoices.Where(o => o.DisplayName == (string)cbVoice.SelectedItem);
            synth.Voice = voice.First();
            synth.Options.AudioPitch = sliderPitch.Value;
            synth.Options.AudioVolume = sliderVolume.Value / 100.0;
            synth.Options.SpeakingRate = sliderSpeed.Value;
            //var options = new SpeechSynthesizerOptions();

            // Send the stream to the media object.
            media.Stop();
            media.AutoPlay = true;
            SpeechSynthesisStream stream = await synth.SynthesizeTextToStreamAsync(contents);
            media.SetSource(stream, stream.ContentType);
            media.Play();
        }

        private async void BtnListen_Click(object sender, RoutedEventArgs e)
        {
            btnListen.IsEnabled = false;
            if (isListening == false)
            {
                if (media != null) media.Stop();
                if (synth != null) synth = null;

                // The recognizer can only start listening in a continuous fashion if the recognizer is currently idle.
                // This prevents an exception from occurring.
                if (recognizer.State == SpeechRecognizerState.Idle)
                {
                    try
                    {
                        await recognizer.ContinuousRecognitionSession.StartAsync();
                        //recognizer.UIOptions.
                        cbLanguageSelection.IsEnabled = false;
                        btnListen.Content = $"{AppResources.GetString("Listen")}...";
                        btnListen.IsChecked = true;
                        btnSpeak.IsChecked = false;
                        BtnCancel.Visibility = Visibility.Visible;
                        isListening = true;
                    }
                    catch (Exception ex)
                    {
                        var messageDialog = new MessageDialog(ex.Message, AppResources.GetString("Exception"));
                        await messageDialog.ShowAsync();
                    }
                }
            }
            else
            {
                isListening = false;
                cbLanguageSelection.IsEnabled = true;
                if (recognizer.State != SpeechRecognizerState.Idle)
                {
                    try
                    {
                        // Cancelling recognition prevents any currently recognized speech from
                        // generating a ResultGenerated event. StopAsync() will allow the final session to 
                        // complete.
                        await recognizer.ContinuousRecognitionSession.CancelAsync();
                        btnListen.Content = AppResources.GetString("Listen");
                        btnListen.IsChecked = false;
                        BtnCancel.Visibility = Visibility.Collapsed;
                    }
                    catch (Exception ex)
                    {
                        var messageDialog = new MessageDialog(ex.Message, AppResources.GetString("Exception"));
                        await messageDialog.ShowAsync();
                    }
                }
            }
            btnListen.IsEnabled = true;
        }

        private async void BtnSaveAs_Click(object sender, RoutedEventArgs e)
        {
            // Generate the audio stream from plain text.
            if (edContent.Text.Length <= 0) return;
            string contents = string.Empty;
            if (edContent.SelectionLength > 0) contents = edContent.SelectedText;
            else if (edContent.SelectionStart >= edContent.Text.Length) contents = edContent.Text;
            else contents = edContent.Text.Substring(edContent.SelectionStart);

            //bool split = (bool)ChkAutoSplit.IsChecked;
            try
            {
                int split = Convert.ToInt32(edSplit.Text.Trim());
                await Text2Audio(contents, split);
            }
            catch (Exception ex)
            {
                MessageDialog msg = new MessageDialog($"{AppResources.GetString("InputError")}: {ex.ToString()}", AppResources.GetString("Error"));
                msg.Commands.Add(new UICommand(AppResources.GetString("OK"), cmd => { }, ContentDialogResult.Primary));
                msg.DefaultCommandIndex = 0;
                msg.CancelCommandIndex = 0;
                await msg.ShowAsync();
            }
        }

    }
}
