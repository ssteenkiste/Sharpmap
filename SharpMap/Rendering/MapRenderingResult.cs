using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using GeoAPI.Geometries;

namespace SharpMap.Rendering
{
    public class MapRenderingResult
    {
        private Image _image = new Bitmap(1, 1);
        private Image _imageBackground = new Bitmap(1, 1);
        private Image _imageStatic = new Bitmap(1, 1);
        private Image _imageVariable = new Bitmap(1, 1);
        private Envelope _imageEnvelope = new Envelope(0, 1, 0, 1);
        private int _imageGeneration;




    }
}
