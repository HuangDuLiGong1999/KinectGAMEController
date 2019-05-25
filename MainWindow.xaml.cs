
namespace Microsoft.Samples.Kinect.KinectGameController
{
    using System;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using System.Threading;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region private attribute
        //因为Kinect分辨率为640*480
        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;
        /// <summary>
        /// 帧数
        /// </summary>
        private int frameNumber = 0;

        #endregion
        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        //边缘检测（检测离哪边近然后用红线标记该边）
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            //画布
            this.drawingGroup = new DrawingGroup();


            this.imageSource = new DrawingImage(this.drawingGroup);


            Image.Source = this.imageSource;

            // 连接Kinect
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // 显示骨骼
                this.sensor.SkeletonStream.Enable();
                this.sensor.ColorStream.Enable();

                // 主要函数!! Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }


        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            //1.更新数据
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            //2.渲染
            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // 黑色背景
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        //渲染边缘
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            //具体渲染
                            this.DrawBonesAndJoints(skel, dc);
                            //------------------------------------------------------//
                            bool r_flag = Armcheck(skel, JointType.WristRight);
                            bool l_flag = Armcheck(skel, JointType.WristLeft);
                            bool oh_flag = OverHeadCheck(skel);
                            if (r_flag)
                            {
                                new Thread(right).Start();
                            }                              
                            if (l_flag)
                            {
                                new Thread(left).Start();
                            }
                            if(oh_flag)
                            {
                                new Thread(run).Start();
                            }
                            frameNumber++;
                            Detection(skel);
                            //-----------------------------------------------------//
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            //模糊渲染
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // 渲染躯干
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);
 
            // 渲染关节
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;                    
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;                    
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point辅助函数寻找位置
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }

        /// <summary>
        /// Handles the checking or unchecking of the seated mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                if (this.checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
        }
        
        //检测函数:为了检测准确请尽量站在合适位置，让Kinect获取全身骨骼点

        ///<summary>
        ///平举检测
        /// </summary>
        private bool Armcheck(Skeleton skeleton, JointType jt)
        {
            float flag = 0;
            if (jt == JointType.WristLeft)
            {
                var wrist = skeleton.Joints[JointType.WristLeft].Position.Y;
                var elbow = skeleton.Joints[JointType.ElbowLeft].Position.Y;
                var shoulder = skeleton.Joints[JointType.ShoulderLeft].Position.Y;
                flag = 2 * shoulder - elbow - wrist;
            }
            else if(jt == JointType.WristRight)
            {
                var wrist = skeleton.Joints[JointType.WristRight].Position.Y;
                var elbow = skeleton.Joints[JointType.ElbowRight].Position.Y;
                var shoulder = skeleton.Joints[JointType.ShoulderRight].Position.Y;
                flag = 2 * shoulder - elbow - wrist;
            }
            if (flag < 0.1f) return true;
            else return false;
        }
        ///<summary>
        ///双手过头检测
        /// </summary>
        private bool OverHeadCheck(Skeleton skeleton)
        {
            var head = skeleton.Joints[JointType.Head].Position.Y;
            var lefthand = skeleton.Joints[JointType.HandLeft].Position.Y;
            var righthand = skeleton.Joints[JointType.HandRight].Position.Y;
            if (lefthand > head && righthand > head) return true;
            else return false;
        }
        #region thread method
        private void left()
        {
            VKB.KeyBoard.keyPress(VKB.KeyBoard.vKeyLeft,1200);
        }
        private void right()
        {
            VKB.KeyBoard.keyPress(VKB.KeyBoard.vKeyRight,1200);
        }
        private void jump()
        {
            VKB.KeyBoard.keyPress(VKB.KeyBoard.vKeyUp,1000);
        }
        private void run()
        {
            VKB.KeyBoard.keyPress(VKB.KeyBoard.vKeyUp,5000);
        }
        #endregion

       
        private float spinemid_xin, spinemid_yin, spinemid_xout, spinemid_yout;
        private float rightfoot_yin, leftfoot_yin, rightfoot_yout, leftfoot_yout;

        

        private float spinebase_yin, rightAnkle_yin, base_foot_in, spinebase_yout, rightAnkle_yout, base_foot_out;
        private float spinemid_x, spinemid_y, rightfoot_y, leftfoot_y, base_foot;
        /// <summary>
        /// 动态动作检测
        /// </summary>
        /// <param name="skeleton"></param>
        private void Detection(Skeleton skeleton)
        {
            if (frameNumber % 11 == 1)      
            {

                spinemid_xin = skeleton.Joints[JointType.Spine].Position.X;
                spinemid_yin = skeleton.Joints[JointType.Spine].Position.Y;
                rightfoot_yin = skeleton.Joints[JointType.KneeRight].Position.Y;
                leftfoot_yin = skeleton.Joints[JointType.KneeLeft].Position.Y;
                spinebase_yin = skeleton.Joints[JointType.Spine].Position.Y;
                rightAnkle_yin = skeleton.Joints[JointType.AnkleRight].Position.Y;
                base_foot_in = spinebase_yin - rightAnkle_yin;

            }
            if (frameNumber % 11 == 0)
            {
 
                spinemid_xout = skeleton.Joints[JointType.Spine].Position.X;
                spinemid_yout = skeleton.Joints[JointType.Spine].Position.Y;
                rightfoot_yout = skeleton.Joints[JointType.KneeRight].Position.Y;
                leftfoot_yout = skeleton.Joints[JointType.KneeLeft].Position.Y;
                spinebase_yout = skeleton.Joints[JointType.Spine].Position.Y;
                rightAnkle_yout = skeleton.Joints[JointType.AnkleRight].Position.Y;

                base_foot_out = spinebase_yout - rightAnkle_yout;
                spinemid_x = spinemid_xout - spinemid_xin;
                spinemid_y = spinemid_yout - spinemid_yin;
                rightfoot_y = rightfoot_yout - rightfoot_yin;
                leftfoot_y = leftfoot_yout - leftfoot_yin;
                base_foot = base_foot_out - base_foot_in;

                if ((leftfoot_y > 0.06 && rightfoot_y > 0.06))        
                {
                    new Thread(jump).Start();
                }

            }

        }


    }
}