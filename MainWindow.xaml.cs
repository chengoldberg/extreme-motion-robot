using System;
using System.Collections.Generic;
using System.Windows;
using System.Text;
using Xtr3D.Net;
using Xtr3D.Net.BaseTypes;
using Xtr3D.Net.ColorImage;
using Xtr3D.Net.AllFrames;
using Xtr3D.Net.ExtremeMotion;
using Xtr3D.Net.ExtremeMotion.Data;
using Xtr3D.Net.ExtremeMotion.Interop.Types;
using Xtr3D.Net.ExtremeMotion.Gesture;
using Xtr3D.Net.Exceptions;
using System.Windows.Media;

namespace CSharpVisualSkeletonSample
{
    class GestureMessage
    {
        public GestureMessage(String _text)
        {
            timeToLiveCounter = 0;
            text = _text;
        }
        public int timeToLiveCounter;
        public String text;
    };

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SkeletonDrawer m_skeletonDrawer;
        private ColorImageDrawer m_colorImageDrawer;
        private ImageInfo m_imageInfo;

        private Dictionary<string, string> warningToString;
        System.IO.StreamWriter file;
        Maestro maestro;

        private Dictionary<BaseGesture.GestureType, int> gestureTypeToDelay = new Dictionary<BaseGesture.GestureType, int>() 
        { 
            {BaseGesture.GestureType.STATIC_POSITION, 1},
            {BaseGesture.GestureType.HEAD_POSITION, 1},
            {BaseGesture.GestureType.SWIPE, 30},
            {BaseGesture.GestureType.WINGS, 1},
            {BaseGesture.GestureType.SEQUENCE, 30},
            {BaseGesture.GestureType.UP, 15},
            {BaseGesture.GestureType.DOWN, 15},
            {BaseGesture.GestureType.RELATIVE_HOT_SPOT, 15},
        };


        private Dictionary<int, GestureMessage> gestureMessages = new Dictionary<int, GestureMessage> { };

        public MainWindow()
        {
            InitializeComponent();
            InitWarningStrings();
            file = new System.IO.StreamWriter(@"ExtremeRobotLog.txt");
            maestro = new Maestro();
            //maestro.TrySetTarget(5, maestro.DegreeToTarget(5, 90));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            m_imageInfo = new ImageInfo(ImageResolution.Resolution1920x1080, Xtr3D.Net.ImageInfo.ImageFormat.RGB888);
            try
            {
                InitGenerator();
            }
            catch (MissingLicenseException)
            {
                SkeletonTrackingState.Text = "Missing License";
                return;
            }
            catch (InvalidLicenseException)
            {
                SkeletonTrackingState.Text = "Invalid License";
                return;
            }
            catch (ExpiredLicenseException)
            {
                SkeletonTrackingState.Text = "Expired License";
                return;
            }
            GeneratorSingleton.Instance.SetGestureRecognitionFile("SamplePoses.xml");
            
            m_skeletonDrawer = new SkeletonDrawer(m_imageInfo);
            SkeletonDisplay.Source = m_skeletonDrawer.ImageSource;
            m_colorImageDrawer = new ColorImageDrawer(m_imageInfo);

          //  AdjustDisplay();
            //Register to the AllFramesReady event with our event handler, which will synchronize between camera and skeleton and draw them on the screen.
            //In case displaying the skeleton is not needed (e.g. calibration stage), using only GeneratorSingleton.Instance.ColorImageFrameReady results in better performance.
            //For that matter, see MyAllFramesReadyEventHandler on how to easily allow switching between waiting for synchronized frames, 
            //coming from all streams (via GeneratorSingleton.Instance.AllFramesReady) 
            //and waiting separately for each stream (GeneratorSingleton.Instance.DataFrameReady and GeneratorSingleton.Instance.ColorImageFrameReady).
            GeneratorSingleton.Instance.AllFramesReady +=
                new EventHandler<AllFramesReadyEventArgs>(MyAllFramesReadyEventHandler);

            // Start event pumping
            GeneratorSingleton.Instance.Start();
            if (CSharpVisualSkeletonSample.Properties.Settings.Default.EnableSmoothing)
            {
                GeneratorSingleton.Instance.DataStream.Enable(new TransformSmoothParameters()
                {
                    Smoothing = CSharpVisualSkeletonSample.Properties.Settings.Default.SmoothingCoeff,
                    Correction = CSharpVisualSkeletonSample.Properties.Settings.Default.CorrectionCoeff,
                    OutlierRemovalSensitivity = CSharpVisualSkeletonSample.Properties.Settings.Default.OutlierRemovalSensitivity,
                    MaxNumberOfConsecutiveRemovals = CSharpVisualSkeletonSample.Properties.Settings.Default.MaxConsecutiveRemovals
                });
            }
        }

        private void InitGenerator()
        {
            //Try to initialize the singleton instance with HD resolution
            try
            {
                GeneratorSingleton.Instance.Initialize(PlatformType.WINDOWS, m_imageInfo);
            }
            catch (GenericEngineErrorException)
            {//If it doesn't succeed, initialize with 640X480
                m_imageInfo = new ImageInfo(ImageResolution.Resolution640x480, Xtr3D.Net.ImageInfo.ImageFormat.RGB888);
                GeneratorSingleton.Instance.Initialize(PlatformType.WINDOWS, m_imageInfo);
            }

        }

        private void AdjustDisplay()
        {
            ColorImageDisplay.Width = m_imageInfo.Width;
            ColorImageDisplay.Height = m_imageInfo.Height;
            SkeletonDisplay.Width = m_imageInfo.Width;
            SkeletonDisplay.Height = m_imageInfo.Height;
        }


        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)        
        {
            maestro.DisableAll();
            file.Close();
            Shutdown();
        }

        private void Shutdown()
        {
            if (GeneratorSingleton.Instance.IsInitialized)
            {
                // Stop pumping
                GeneratorSingleton.Instance.AllFramesReady -= MyAllFramesReadyEventHandler;
                GeneratorSingleton.Instance.Stop();

                // Conditionally shutdown the service
                GeneratorSingleton.Instance.Shutdown();
            }
        }

        #region Framework Event Handling

        //This is the handler to be given to GeneratorSingleton.Instance.AllFramesReady.
        //In order to easily switch to waiting separately for each stream 
        //(GeneratorSingleton.Instance.DataFrameReady and GeneratorSingleton.Instance.ColorImageFrameReady), 
        //The issue the following, instead of MyAllFramesReadyEventHandler:
        // GeneratorSingleton.Instance.DataFrameReady += JointsUpdate.MyDataFrameReady;
        // GeneratorSingleton.Instance.ColorImageFrameReady += RgbTextureUpdate.MyColorImageFrameReadyEventHandler;
        private void MyAllFramesReadyEventHandler(object sender, AllFramesReadyEventArgs e)
        {
            using (var allFrames = e.OpenFrame() as AllFramesFrame)
            {
                if (allFrames != null)
                {
                    foreach (var evtArgs in allFrames.FramesReadyEventArgs)
                    {
                        var colorImageFrameReady = evtArgs as ColorImageFrameReadyEventArgs;
                        if (null != colorImageFrameReady)
                        {
                            this.MyColorImageFrameReadyEventHandler(sender, colorImageFrameReady);
                            continue;
                        }
                        var dataFrameReady = evtArgs as DataFrameReadyEventArgs;
                        if (null != dataFrameReady)
                        {
                            this.MyDataFrameReady(sender, dataFrameReady);
                            continue;
                        }
                        var gestureFrameReady = evtArgs as GesturesFrameReadyEventArgs;
                        if (null != gestureFrameReady)
                        {
                            this.MyRecognitionFrameReadyEventHandler(sender, gestureFrameReady);
                            continue;
                        }
                    }
                    LogWarnings(allFrames.FrameKey.FrameNumberKey);
                }
            }
        }

        private void MyRecognitionFrameReadyEventHandler(object sender, GesturesFrameReadyEventArgs e)
        {
            // Opening the received frame
            using (var gesturesFrame = e.OpenFrame() as GesturesFrame)
            {
                GesturesText.Text = "";
                if (gesturesFrame != null)
                {
                    StringBuilder gesturesString = new StringBuilder();
                    gesturesString.AppendFormat("Gestures frame: {0}, contains {1} gestures",
                        gesturesFrame.FrameKey.FrameNumberKey, gesturesFrame.FirstSkeletonGestures().Length);
                    Console.WriteLine(gesturesString);
                    int i = 0;
                    foreach (BaseGesture gesture in gesturesFrame.FirstSkeletonGestures())
                    {
                        // Update messages for gesture
                        if (!gestureMessages.ContainsKey(gesture.ID))
                        {
                            gestureMessages.Add(gesture.ID, new GestureMessage(gesture.Description));
                        }
                        gestureMessages[gesture.ID].timeToLiveCounter = gestureTypeToDelay[gesture.Type];
                        switch (gesture.Type)
                        {
                            case BaseGesture.GestureType.HEAD_POSITION:
                                {
                                    HeadPositionGesture headPositionGesture = gesture as HeadPositionGesture;
                                    gestureMessages[gesture.ID].text = gesture.Description + " (" + headPositionGesture.RegionIndex+ ")";
                                    break;
                                }
                            case BaseGesture.GestureType.WINGS:
                                {
                                    WingsGesture wingsGesture = gesture as WingsGesture;
                                    gestureMessages[gesture.ID].text = gesture.Description + " (" + wingsGesture.ArmsAngle + ")";
                                    break;
                                }
                        }
                        Console.WriteLine("{0}. Gesture id: {1} - {2}", i, gesture.ID, gestureMessages[gesture.ID].text);//written to console for automatic tests
                        i++;
                    }
                }

                // Generate gestures text
                GesturesText.Text = "";
                foreach (int id in gestureMessages.Keys)
                {
                    if (gestureMessages[id].timeToLiveCounter > 0)
                    {
                        GesturesText.Text += id + " - " + gestureMessages[id].text;
                        GesturesText.Text += "\r\n";
                        gestureMessages[id].timeToLiveCounter--;
                    }
                }
            }
        }

        private void MyColorImageFrameReadyEventHandler(object sender, ColorImageFrameReadyEventArgs e)
        {
            // Opening the received frame
            using (var colorImageFrame = e.OpenFrame() as ColorImageFrame)
            {
                if (colorImageFrame != null) // Making sure it's really ColorImageFrame
                {
                    Console.WriteLine("Raw image frame: " + colorImageFrame.FrameKey.FrameNumberKey);
                    m_colorImageDrawer.DrawColorImage(colorImageFrame.ColorImage.Image); // Reading the ColorImage data
                    ColorImageDisplay.Source = m_colorImageDrawer.ImageSource;

                    var colorImageStream = colorImageFrame.Stream as ImageStreamBase<FrameKey, ColorImage>;
                    UpdateImageAndVideoWarnings(colorImageFrame.Warnings, colorImageStream.Warnings); // Reading both Image and ImageStream warnings
                }
            }
        }

        void MyDataFrameReady(object sender, Xtr3D.Net.ExtremeMotion.Data.DataFrameReadyEventArgs e)
        {
            // Opening the received frame
            using (var dataFrame = e.OpenFrame() as DataFrame)
            {

                if (dataFrame != null) // Making sure it's really DataFrame
                {
                    StringBuilder text = new StringBuilder();
                    Skeleton skel = dataFrame.Skeletons[0];
                    text.AppendFormat("Skeleton frame: {0}, state: {1}, proximity: {2}",
                        dataFrame.FrameKey.FrameNumberKey, skel.TrackingState.ToString(), skel.Proximity.SkeletonProximity);
                    //Console.WriteLine(text);//written to console for automatic tests
                    if (dataFrame.Skeletons[0] != null)
                    {
                    	var joints = dataFrame.Skeletons[0].Joints; // Possibly several Skeletons, we'll use the first
                        TrackingState state = dataFrame.Skeletons[0].TrackingState;
                        SkeletonTrackingState.Text = state.ToString();
                        SetCalibrationIconVisibility(state);
	                    // We only want to display a tracked Skeleton
	                    if (joints.Head.jointTrackingState == JointTrackingState.Tracked)
	                    {
	                        m_skeletonDrawer.DrawSkeleton(joints);
	                    }
	                    else
	                    {
	                        m_skeletonDrawer.WipeSkeleton();
                            maestro.DisableAll();
	                    }
                        
                        if(state == TrackingState.Tracked)
                        {
                            double L0 = Math.Atan2(
                                joints.ElbowLeft.skeletonPoint.Y - joints.ShoulderLeft.skeletonPoint.Y,
                                -1 * joints.ElbowLeft.skeletonPoint.X - -1 * joints.ShoulderLeft.skeletonPoint.X) * 180.0 / Math.PI;

                            double L1 = Math.Atan2(
                                joints.HandLeft.skeletonPoint.Y - joints.ElbowLeft.skeletonPoint.Y,
                                -1 * joints.HandLeft.skeletonPoint.X - -1 * joints.ElbowLeft.skeletonPoint.X) * 180.0 / Math.PI;

                            double R0 = Math.Atan2(
                                joints.ElbowRight.skeletonPoint.Y - joints.ShoulderRight.skeletonPoint.Y,
                                joints.ElbowRight.skeletonPoint.X - joints.ShoulderRight.skeletonPoint.X) * 180.0 / Math.PI;

                            double R1 = Math.Atan2(
                                joints.HandRight.skeletonPoint.Y - joints.ElbowRight.skeletonPoint.Y,
                                joints.HandRight.skeletonPoint.X - joints.ElbowRight.skeletonPoint.X) * 180.0 / Math.PI;

                            StringBuilder text2 = new StringBuilder();
                            text2.AppendFormat("{4} L0:{0},R0:{1},L1:{2},R1:{3} \t",
                                Math.Round(L0), Math.Round(R0), Math.Round(L1) - Math.Round(L0), Math.Round(R1) - Math.Round(R0), DateTime.UtcNow.ToString("yy-MM-ddThh:mm:ss"));

                            ushort TL0 = maestro.DegreeToTarget(1, (int) Math.Round(L0));
                            ushort TR0 = maestro.DegreeToTarget(2, (int) Math.Round(R0));
                            ushort TL1 = maestro.DegreeToTarget(4, (int) (Math.Round(L1) - Math.Round(L0)));
                            ushort TR1 = maestro.DegreeToTarget(5, (int) (Math.Round(R1) - Math.Round(R0)));
                            text2.AppendFormat("TL0:{0},TR0:{1},TL1:{2},TR1:{3}",
                                TL0, TR0, TL1, TR1);

                            file.WriteLine(text2);//written to console for automatic tests                            

                            maestro.TrySetTarget(1, TL0);
                            maestro.TrySetTarget(2, TR0);
                            maestro.TrySetTarget(4, TL1);
                            maestro.TrySetTarget(5, TR1);
                        }

	                    UpdateFrameEdges(dataFrame.Skeletons[0].ClippedEdges); // Reading the Skeleton Edge warnings
                    }
                }
            }
        }

        private void SetCalibrationIconVisibility(TrackingState state)
        {
            if (state == Xtr3D.Net.ExtremeMotion.Data.TrackingState.Calibrating)
            {
                CalibrationIcon.Visibility = Visibility.Visible;
            }
            else
            {
                CalibrationIcon.Visibility = Visibility.Hidden;
            }
        }
        #endregion

        #region Warnings Update
        private void UpdateImageAndVideoWarnings(ImageWarnings imageWarnings, ImageStreamWarnings imageStreamWarnings)
        {
            // Using .NET Enum functions to see what Warnings we received
            LightLowBox.IsChecked = imageWarnings.HasFlag(ImageWarnings.LightLow);
            StrongBacklightingBox.IsChecked = imageWarnings.HasFlag(ImageWarnings.StrongBacklighting);
            TooManyPeople.IsChecked = imageWarnings.HasFlag(ImageWarnings.TooManyPeople);
        }

        private void UpdateFrameEdges(FrameEdges edges)
        {
            // Using .NET Enum functions to see what Warnings we received                        
            Right.IsChecked = edges.HasFlag(FrameEdges.Right);
            Near.IsChecked = edges.HasFlag(FrameEdges.Near);
            Left.IsChecked = edges.HasFlag(FrameEdges.Left);
            Far.IsChecked = edges.HasFlag(FrameEdges.Far);
        }
        #endregion

        void LogWarnings(long frameId)
        {
            StringBuilder warningsListText = new StringBuilder();
            int counter = 0;
            string str = "";
            if (LightLowBox.IsChecked.Value)
            {
                warningToString.TryGetValue(ImageWarnings.LightLow.ToString(), out str);
                warningsListText.Append("\n - " + str);
                counter++;
            }
            if (StrongBacklightingBox.IsChecked.Value)
            {
                warningToString.TryGetValue(ImageWarnings.StrongBacklighting.ToString(), out str);
                warningsListText.Append("\n - " + str);
                counter++;
            }
            if (TooManyPeople.IsChecked.Value)
            {
                warningToString.TryGetValue(ImageWarnings.TooManyPeople.ToString(), out str);
                warningsListText.Append("\n - " + str);
                counter++;
            }
            if (Right.IsChecked.Value)
            {
                warningToString.TryGetValue(FrameEdges.Right.ToString(), out str);
                warningsListText.Append("\n - " + str);
                counter++;
            }
            if (Near.IsChecked.Value)
            {
                warningToString.TryGetValue(FrameEdges.Near.ToString(), out str);
                warningsListText.Append("\n - " + str);
                counter++;
            }
            if (Left.IsChecked.Value)
            {
                warningToString.TryGetValue(FrameEdges.Left.ToString(), out str);
                warningsListText.Append("\n - " + str);
                counter++;
            }
            if (Far.IsChecked.Value)
            {
                warningToString.TryGetValue(FrameEdges.Far.ToString(), out str);
                warningsListText.Append("\n - " + str);
                counter++;
            }
            StringBuilder warningsText = new StringBuilder();
            warningsText.AppendFormat("Warnings frame: {0}, contains {1} warnings:{2}", frameId, counter, warningsListText);
            Console.WriteLine(warningsText);//written to console for automatic tests
        }

        private void ResetClick(object sender, RoutedEventArgs e)
        {
            // Reset engine
            GeneratorSingleton.Instance.Reset();
            // Reset display
            ResetDisplay();
        }

        private void ResetDisplay()
        {

            UpdateFrameEdges(FrameEdges.None);
            UpdateImageAndVideoWarnings(ImageWarnings.None, ImageStreamWarnings.None);
        }

        private void InitWarningStrings()
        {
            warningToString = new Dictionary<string, string>();
            warningToString.Add(ImageWarnings.LightLow.ToString(), "Low lighting");
            warningToString.Add(ImageWarnings.StrongBacklighting.ToString(), "Strong backlighting");
            warningToString.Add(ImageWarnings.TooManyPeople.ToString(), "Too many people");
            warningToString.Add(FrameEdges.Far.ToString(), "Too far");
            warningToString.Add(FrameEdges.Near.ToString(), "Too close");
            warningToString.Add(FrameEdges.Left.ToString(), "Too left");
            warningToString.Add(FrameEdges.Right.ToString(), "Too right");
        }


    }


}
