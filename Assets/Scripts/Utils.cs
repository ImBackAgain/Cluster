namespace Utils
{
    public static class Constants
    {
        public const int BLENDER_FPS = 60;
        public const float ALMOST_TIME = 1f/600;
        //I think this should correspond to one frame of world time with maximum slowdown.
        //I.e. 1 / (fps * max slowdown)
    }
}