using SpeechAndTTS;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources.Core;
using Windows.Foundation;
using Windows.Globalization;
using Windows.Graphics.Display;
using Windows.Media.SpeechRecognition;
using Windows.Media.SpeechSynthesis;
using Windows.UI.Core;
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
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        private static int sizeMinW = 960;
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

        public MainPage()
        {
            this.InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Set<T>(ref T storage, T value, [CallerMemberName]string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return;
            }

            storage = value;
            OnPropertyChanged(propertyName);
        }

        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

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

        private async void BtnSpeak_Click(object sender, RoutedEventArgs e)
        {
            if (synth == null) synth = new SpeechSynthesizer();
            //using (SpeechSynthesizer synth = new SpeechSynthesizer())
            {
                var voice = SpeechSynthesizer.AllVoices.Where(o => o.DisplayName == (string)cbVoice.SelectedItem);
                synth.Voice = voice.First();
                synth.Options.AudioPitch = sliderPitch.Value;
                synth.Options.AudioVolume = sliderVolume.Value / 100.0;
                synth.Options.SpeakingRate = sliderSpeed.Value;
                //var options = new SpeechSynthesizerOptions();

                // Generate the audio stream from plain text.
                string contents = string.Empty;
                if (edContent.SelectionLength > 0) contents = edContent.SelectedText;
                else if (edContent.SelectionStart >= edContent.Text.Length) contents = edContent.Text;
                else contents = edContent.Text.Substring(edContent.SelectionStart);
                SpeechSynthesisStream stream = await synth.SynthesizeTextToStreamAsync(contents);

                // Send the stream to the media object.
                media.Stop();
                media.AutoPlay = true;
                media.SetSource(stream, stream.ContentType);
                media.Play();
            }
        }

        private async void BtnListen_Click(object sender, RoutedEventArgs e)
        {
            btnListen.IsEnabled = false;
            if (isListening == false)
            {
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
                        isListening = true;
                    }
                    catch (Exception ex)
                    {
                        var messageDialog = new Windows.UI.Popups.MessageDialog(ex.Message, AppResources.GetString("Exception"));
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
                    }
                    catch (Exception ex)
                    {
                        var messageDialog = new Windows.UI.Popups.MessageDialog(ex.Message, AppResources.GetString("Exception"));
                        await messageDialog.ShowAsync();
                    }
                }
            }
            btnListen.IsEnabled = true;

            //// Initialize resource map to retrieve localized speech strings.
            //Language speechLanguage = SpeechRecognizer.SystemSpeechLanguage;
            //string langTag = speechLanguage.LanguageTag;
            //speechContext = ResourceContext.GetForCurrentView();
            //speechContext.Languages = new string[] { langTag };

            //speechResourceMap = ResourceManager.Current.MainResourceMap.GetSubtree("LocalizationSpeechResources");

            //PopulateLanguageDropdown();
            //await InitializeRecognizer(SpeechRecognizer.SystemSpeechLanguage);

            ////using (SpeechRecognizer recognizer = new SpeechRecognizer())
            //{
            //    //var session = recognizer.ContinuousRecognitionSession;
            //    //await session.StopAsync();
            //    //await session.StartAsync();
            //    //SpeechRecognitionResult result = await recognizer.RecognizeWithUIAsync();

            //    SpeechRecognitionResult result = await recognizer.RecognizeAsync();

            //    this.edContent.Text += result.Text;
            //}
        }

        private async void cbLanguageSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isPopulatingLanguages) return;
            Language targetLang = (Language)(cbLanguageSelection.SelectedItem as ComboBoxItem).Tag;
            await InitializeRecognizer(targetLang);
            edHearState.Text = AppResources.GetString("Idle");
        }

    }
}
