using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AMDiscordRPC
{
    internal class S3
    {
        private static IAmazonS3 s3Client;

        public static void InitS3()
        {
            BasicAWSCredentials credentials = new BasicAWSCredentials("accessKey-Id", "secretKey");
            s3Client = new AmazonS3Client(credentials, new AmazonS3Config
            {
                ServiceURL = "S3/R2 Bucket url",
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            });
        }

        public static async Task<string> PutGIF(string path, string filename)
        {
            PutObjectRequest request = new PutObjectRequest
            {
                FilePath = @path,
                BucketName = "bucket-name",
                DisablePayloadSigning = true
            };

            PutObjectResponse response = await s3Client.PutObjectAsync(request);

            return $"https://your.domain.com/{filename}";
        }
    }
}
