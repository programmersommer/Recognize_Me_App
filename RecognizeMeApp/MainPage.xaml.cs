using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;


using Microsoft.ProjectOxford.SpeakerRecognition;
using Microsoft.ProjectOxford.SpeakerRecognition.Contract;
using Microsoft.ProjectOxford.SpeakerRecognition.Contract.Verification;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using System.Threading.Tasks;

namespace RecognizeMeApp
{

    public sealed partial class MainPage : Page
    {
        private SpeakerVerificationServiceClient _serviceClient;
        private string _subscriptionKey;
        Guid _speakerId;

        DispatcherTimer _etimer;
        DispatcherTimer _vtimer;

        private MediaCapture CaptureMedia;
        private IRandomAccessStream AudioStream;

        public MainPage()
        {
            this.InitializeComponent();

            CaptureMedia = null;

            _etimer = new DispatcherTimer();
            _etimer.Interval = new TimeSpan(0, 0, 10);
            _etimer.Tick += EnrollmentTime_Over;

            _vtimer = new DispatcherTimer();
            _vtimer.Interval = new TimeSpan(0, 0, 10);
            _vtimer.Tick += VerificationTime_Over;

            _subscriptionKey = "put_your_subscription_key_here";
            _serviceClient = new SpeakerVerificationServiceClient(_subscriptionKey);

        }


        private async void btnRecordEnroll_Click(object sender, RoutedEventArgs e)
        {
            txtInfo.Text = "";

            if (lbProfiles.SelectedIndex < 0)
            {
                txtInfo.Text = "Get profiles select one of them";
                return;
            }

            if (CaptureMedia == null)
            {
                btnVerify.IsEnabled = false;
                btnRecordEnroll.Content = "Stop record enrollment";

                CaptureMedia = new MediaCapture();
                var captureInitSettings = new MediaCaptureInitializationSettings();
                captureInitSettings.StreamingCaptureMode = StreamingCaptureMode.Audio;
                await CaptureMedia.InitializeAsync(captureInitSettings);
                MediaEncodingProfile encodingProfile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);
                encodingProfile.Audio.ChannelCount = 1;
                encodingProfile.Audio.SampleRate = 16000;
                AudioStream = new InMemoryRandomAccessStream();
                CaptureMedia.RecordLimitationExceeded += MediaCaptureOnRecordLimitationExceeded;
                CaptureMedia.Failed += MediaCaptureOnFailed;
                await CaptureMedia.StartRecordToStreamAsync(encodingProfile, AudioStream);

                _etimer.Start();

            }
            else
            {
                _etimer.Stop();
                await finishEnrollment();
            }

        }


        async Task finishEnrollment()
        {

            _speakerId = Guid.Parse((lbProfiles.SelectedItem as ListBoxItem).Content.ToString());

            btnRecordEnroll.Content = "Start record enrollment";
            btnRecordEnroll.IsEnabled = false;
            await CaptureMedia.StopRecordAsync();

            Stream str = AudioStream.AsStream();
            str.Seek(0, SeekOrigin.Begin);

            Enrollment response;

            try
            {
                response = await _serviceClient.EnrollAsync(str, _speakerId);
            }
            catch (EnrollmentException ex)
            {
                txtInfo.Text = ex.Message;
                CleanAfter();
                return;
            }

            txtInfo.Text = "Remaining enrollments: " + txtInfo.Text + response.RemainingEnrollments.ToString();
            txtInfo.Text = txtInfo.Text + Environment.NewLine + response.Phrase;

            CleanAfter();
        }


        private async void EnrollmentTime_Over(object sender, object e)
        {
            _etimer.Stop();
            await finishEnrollment();
        }

        private async void VerificationTime_Over(object sender, object e)
        {
            _vtimer.Stop();
            await finishVerification();
        }

        private void MediaCaptureOnRecordLimitationExceeded(MediaCapture sender)
        {
            txtInfo.Text = "Record limitation exceeded";
            CleanAfter();
        }

        private void MediaCaptureOnFailed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            txtInfo.Text = errorEventArgs.Message;
            CleanAfter();
        }

        private async void btnVerify_Click(object sender, RoutedEventArgs e)
        {
            txtInfo.Text = "";

            if (lbProfiles.SelectedIndex < 0)
            {
                txtInfo.Text = "Get profiles and select one of them";
                return;
            }


            if (CaptureMedia == null)
            {
                btnVerify.Content = "Stop voice verification";
                btnRecordEnroll.IsEnabled = false;

                CaptureMedia = new MediaCapture();
                var captureInitSettings = new MediaCaptureInitializationSettings();
                captureInitSettings.StreamingCaptureMode = StreamingCaptureMode.Audio;
                await CaptureMedia.InitializeAsync(captureInitSettings);
                MediaEncodingProfile encodingProfile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);
                encodingProfile.Audio.ChannelCount = 1;
                encodingProfile.Audio.SampleRate = 16000;
                AudioStream = new InMemoryRandomAccessStream();
                CaptureMedia.RecordLimitationExceeded += MediaCaptureOnRecordLimitationExceeded;
                CaptureMedia.Failed += MediaCaptureOnFailed;
                await CaptureMedia.StartRecordToStreamAsync(encodingProfile, AudioStream);

                _vtimer.Start();
            }
            else
            {
                _vtimer.Stop();
                await finishVerification();
            }

        }

        async Task finishVerification()
        {
            _speakerId = Guid.Parse((lbProfiles.SelectedItem as ListBoxItem).Content.ToString());

            btnVerify.Content = "Start voice verification";
            btnVerify.IsEnabled = false;
            await CaptureMedia.StopRecordAsync();

            Stream str = AudioStream.AsStream();
            str.Seek(0, SeekOrigin.Begin);

            Verification response;

            try
            {
                response = await _serviceClient.VerifyAsync(str, _speakerId);
            }
            catch (VerificationException vx)
            {
                txtInfo.Text = vx.Message;
                CleanAfter();
                return;
            }
            if (response.Result == Result.Accept)
            {
                txtInfo.Text = "Identity verified" + Environment.NewLine + response.Phrase;
            }
            else
            {
                txtInfo.Text = "Not verified" + Environment.NewLine + response.Phrase;
            }

            CleanAfter();
        }

        void CleanAfter()
        {
            CaptureMedia = null;
            btnRecordEnroll.IsEnabled = true;
            btnVerify.IsEnabled = true;
        }

        private async void btnGetProfiles_Click(object sender, RoutedEventArgs e)
        {
            await GetProfiles();
        }

        async Task GetProfiles()
        {

            try // I don't want to disable buttons and for multiple clicks error catching
            {
                Profile[] profiles = await _serviceClient.GetProfilesAsync();

                lbProfiles.Items.Clear();
                foreach (Profile _profile in profiles)
                {
                    ListBoxItem lbi = new ListBoxItem();
                    lbi.Content = _profile.ProfileId;
                    lbProfiles.Items.Add(lbi);
                }
            }
            catch { }
        }

        private async void btnGetPhrases_Click(object sender, RoutedEventArgs e)
        {
            await GetPhrases();
        }

        async Task GetPhrases()
        {

            try // I don't want to disable buttons and for multiple clicks error catching
            {
                VerificationPhrase[] phrases = await _serviceClient.GetPhrasesAsync("en-us");

                lbPhrases.Items.Clear();
                foreach (VerificationPhrase phrase in phrases)
                {
                    ListBoxItem lbi = new ListBoxItem();
                    lbi.Content = phrase.Phrase;
                    lbPhrases.Items.Add(lbi);
                }
            }
            catch { }
        }

        private async void btnResetEnroll_Click(object sender, RoutedEventArgs e)
        {
            txtInfo.Text = "";

            if (lbProfiles.SelectedIndex < 0)
            {
                txtInfo.Text = "Get profiles and select one of them";
                return;
            }

            _speakerId = Guid.Parse((lbProfiles.SelectedItem as ListBoxItem).Content.ToString());

            try
            {
                await _serviceClient.ResetEnrollmentsAsync(_speakerId);
                txtInfo.Text = "Enrollments are cleaned";
            }
            catch { }
        }

        private async void btnCreateProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CreateProfileResponse response = await _serviceClient.CreateProfileAsync("en-us");
                txtInfo.Text = "Profile Created: " + response.ProfileId;
            }
            catch { }

            await GetProfiles();
        }


        private async void btnRemoveProfile_Click(object sender, RoutedEventArgs e)
        {
            txtInfo.Text = "";

            if (lbProfiles.SelectedIndex < 0)
            {
                txtInfo.Text = "Get and select profile at first";
                return;
            }

            _speakerId = Guid.Parse((lbProfiles.SelectedItem as ListBoxItem).Content.ToString());

            try
            {
                await _serviceClient.DeleteProfileAsync(_speakerId);
                txtInfo.Text = "Profile deleted";
            }
            catch { }

            await GetProfiles();
        }


    }
}
