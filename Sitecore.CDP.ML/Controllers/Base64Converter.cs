using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;


namespace WebGender.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class Base64Converter : ControllerBase
    {
        [HttpPost]
        public string ImageToBase64(IFormFile file)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                file.CopyTo(stream);
                byte[] _imageBytes = stream.ToArray();
                var _base64String = Convert.ToBase64String(_imageBytes);

                return $"data:{file.ContentType};base64," + _base64String;
            }
        }
    }
}
