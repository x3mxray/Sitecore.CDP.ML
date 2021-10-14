using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Dnn;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Drawing;
using System.IO;

namespace WebGender.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PhotoAnalyzeController : ControllerBase
    {
        static string protoGender = @"deploy_gender.prototxt";
        static string caffeGender = @"gender_net.caffemodel";

        static string protoAge = @"deploy_age.prototxt";
        static string caffeAge = @"age_net.caffemodel";
        static string cascade = @"haarcascade_frontalface_alt.xml";

        static MCvScalar mean = new MCvScalar(78.4263377603, 87.7689143744, 114.895847746);

        static Net netGender;
        static Net netAge;
        static CascadeClassifier faceDetector;

        private readonly ILogger<PhotoAnalyzeController> _logger;
        private readonly IHostEnvironment _hostEnvironment;

        public PhotoAnalyzeController(ILogger<PhotoAnalyzeController> logger, IHostEnvironment hostEnvironment)
        {
            _logger = logger;
            _hostEnvironment = hostEnvironment;
            if (netAge == null || netGender == null)
            {
                var path = _hostEnvironment.ContentRootPath;

                netGender = DnnInvoke.ReadNetFromCaffe(Path.Combine(path, protoGender), Path.Combine(path, caffeGender));
                netAge = DnnInvoke.ReadNetFromCaffe(Path.Combine(path, protoAge), Path.Combine(path, caffeAge));
                faceDetector = new CascadeClassifier(Path.Combine(path, cascade));
            }
        }



        //[HttpPost]
        //public MlResponse Detect(IFormFile file)
        //{

        //    string filePath;
        //    using (var memoryStream = new MemoryStream())
        //    {
        //        filePath = Path.GetTempFileName();
        //        using (var stream = System.IO.File.Create(filePath))
        //        {
        //            file.CopyTo(stream);
        //        }
        //    }

        //    var response = AnalizeImage(filePath);

        //    return response;
        //}

       

        [HttpPost]
        public MlResponse DetectBase64(MlRequest image)
        {
            try
            {
                string filePath = Path.GetTempFileName();
                var baseString = image.base64.Split(";base64,")[1];
                System.IO.File.WriteAllBytes(filePath, Convert.FromBase64String(baseString));
                var response = AnalyzeImage(filePath);

                return response;
            }
            catch
            {
                return new MlResponse { gender = "unknown" };
            }
        }


        [NonAction]
        public MlResponse AnalyzeImage(string imageFile)
        {
            var image = new Image<Bgr, byte>(imageFile);
            var result = new MlResponse
            {
                gender = "unknown"
            };

            var imgGray = new UMat();

            CvInvoke.CvtColor(image, imgGray, ColorConversion.Bgr2Gray);

            var faces = faceDetector.DetectMultiScale(
                         imgGray,
                         1.1,
                         10,
                         new Size(20, 20),
                         Size.Empty);

            //// If single person face found on image
            if (faces.Length == 1)
            {
                result.detected = true;

                var blob = DnnInvoke.BlobFromImage(image, 1, swapRB: false, mean: mean, size: new Size(227, 227));
                netGender.SetInput(blob);
                netAge.SetInput(blob);

                var vector = new VectorOfMat();
                netGender.Forward(vector, netGender.UnconnectedOutLayersNames);

                var data = vector[0].GetData();
                var men = ((float[,])data)[0, 0];
                var woman = ((float[,])data)[0, 1];

                var vectorAge = new VectorOfMat();
                netAge.Forward(vectorAge, netGender.UnconnectedOutLayersNames);

                var age_list = new[] { "0-2", "4-6", "8-12", "15-20", "25-32", "38-43", "48-53", "60-100" };
                var ageData = vectorAge[0].GetData();

                var index = 0;
                float max = 0;
                for (var i = 0; i < ageData.Length; i++)
                {
                    var current = ((float[,])ageData)[0, i];
                    if (current > max)
                    {
                        max = current;
                        index = i;
                    }
                }

                result.gender = men > woman ? "male" : "female";
                var range = age_list[index];
                result.age_min = int.Parse(range.Split("-")[0]);
                result.age_max = int.Parse(range.Split("-")[1]);
            }

            return result;

        }
    }


    public class MlResponse
    {
        public bool detected { get; set; }
        public int age_min { get; set; }
        public int age_max { get; set; }
        public string gender { get; set; }
    }

    public class MlRequest
    {
        public string base64 { get; set; }
    }
}
