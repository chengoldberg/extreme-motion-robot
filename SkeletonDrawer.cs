using System.Windows.Media;
using Xtr3D.Net.ExtremeMotion.Data;
using Xtr3D.Net.ExtremeMotion.Interop.Types;
using Xtr3D.Net;

namespace CSharpVisualSkeletonSample
{
    class SkeletonDrawer
    {
        private readonly Brush m_brush = new SolidColorBrush(Color.FromArgb(255, 70, 190, 70));
        private Pen m_bonePen = new Pen(Brushes.Green, 8);
        DrawingGroup m_drawingGroup;
        DrawingImage m_imageSource;
        ImageInfo m_imageInfo;

        public SkeletonDrawer(ImageInfo imageInfo)
        {
            m_drawingGroup = new DrawingGroup();
            m_imageSource = new DrawingImage(m_drawingGroup);
            m_imageInfo = imageInfo;
        }

        public ImageSource ImageSource
        {
            get
            {
                return m_imageSource;
            }
        }

        internal System.Windows.Point toScreenPoint(Joint joint)
        {
            double calcedX = joint.skeletonPoint.ImgCoordNormHorizontal * m_imageInfo.Width,
                calcedY = joint.skeletonPoint.ImgCoordNormVertical * m_imageInfo.Height;
            double x = calcedX >= m_imageInfo.Width ? m_imageInfo.Width - 1 : calcedX;
            double y = calcedY >= m_imageInfo.Height ? m_imageInfo.Height - 1 : calcedY;

            return new System.Windows.Point(x, y);
        }

        internal void DrawBone(DrawingContext dc, Joint joint1, Joint joint2)
        {
            dc.DrawLine(m_bonePen, toScreenPoint(joint1), toScreenPoint(joint2));
            DrawJoint(dc, joint1);
            DrawJoint(dc, joint2);
        }

        internal void WipeSkeleton()
        {
            using (DrawingContext dc = m_drawingGroup.Open())
            {
                dc.DrawEllipse(m_brush, null, new System.Windows.Point(0, 0), 1, 1);
                dc.DrawEllipse(m_brush, null, new System.Windows.Point(m_imageInfo.Width, m_imageInfo.Height), 1, 1);
            }
        }

        internal void DrawJoint(DrawingContext dc, Joint joint1)
        {
            dc.DrawEllipse(m_brush, null, toScreenPoint(joint1), 3, 3);
        }        

        internal void DrawSkeleton(JointCollection joints)
        {
            using (DrawingContext dc = m_drawingGroup.Open())
            {
                dc.DrawEllipse(m_brush, null, new System.Windows.Point(0, 0), 1, 1);
                dc.DrawEllipse(m_brush, null, new System.Windows.Point(m_imageInfo.Width, m_imageInfo.Height), 1, 1);

                DrawJoint(dc, joints.Head);

                DrawBone(dc, joints.Spine, joints.HipCenter);
                DrawBone(dc, joints.Head, joints.ShoulderCenter);
                DrawBone(dc, joints.ShoulderCenter, joints.Spine);
                DrawBone(dc, joints.Spine, joints.ShoulderLeft);
                DrawBone(dc, joints.Spine, joints.ShoulderRight);

                if (joints.ElbowLeft.jointTrackingState == JointTrackingState.NotTracked)
                {
                    DrawJoint(dc, joints.ShoulderLeft);
                }
                else
                {
                    DrawBone(dc, joints.ShoulderLeft, joints.ElbowLeft);

                    if (joints.HandLeft.jointTrackingState != JointTrackingState.NotTracked)
                    {
                        DrawBone(dc, joints.ElbowLeft, joints.HandLeft);
                    }
                }

                if (joints.ElbowRight.jointTrackingState == JointTrackingState.NotTracked)
                {
                    DrawJoint(dc, joints.ShoulderRight);
                }
                else
                {
                    DrawBone(dc, joints.ShoulderRight, joints.ElbowRight);

                    if (joints.HandRight.jointTrackingState != JointTrackingState.NotTracked)
                    {
                        DrawBone(dc, joints.ElbowRight, joints.HandRight);
                    }
                }

                // Lower Body
                if (joints.KneeLeft.jointTrackingState == JointTrackingState.NotTracked)
                {
                    DrawJoint(dc, joints.HipLeft);
                }
                else
                {
                    DrawBone(dc, joints.HipLeft, joints.KneeLeft);

                    if (joints.FootLeft.jointTrackingState != JointTrackingState.NotTracked)
                    {
                        DrawBone(dc, joints.KneeLeft, joints.FootLeft);
                    }
                }

                if (joints.KneeRight.jointTrackingState == JointTrackingState.NotTracked)
                {
                    DrawJoint(dc, joints.HipRight);
                }
                else
                {
                    DrawBone(dc, joints.HipRight, joints.KneeRight);

                    if (joints.FootRight.jointTrackingState != JointTrackingState.NotTracked)
                    {
                        DrawBone(dc, joints.KneeRight, joints.FootRight);
                    }
                }                  
            }
        }
    }
}
