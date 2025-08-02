using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using AMDiscordRPC.UIComponents;
using System;
using System.Threading.Tasks;
using static AMDiscordRPC.Globals;

namespace AMDiscordRPC
{
    internal class S3
    {
        private static IAmazonS3 s3Client;

        public static void InitS3()
        {
            try
            {
                if (S3_Credentials != null && S3_Credentials.GetNullKeys().Count == 0)
                {
                    BasicAWSCredentials credentials = new BasicAWSCredentials(S3_Credentials.accessKey, S3_Credentials.secretKey);
                    s3Client = new AmazonS3Client(credentials, new AmazonS3Config
                    {
                        ServiceURL = S3_Credentials.serviceURL,
                        RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                        ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
                    });
                    S3Status = S3ConnectionStatus.Connected;
                    InputWindow.ChangeS3Status(S3ConnectionStatus.Connected);
                }
                else
                {
                    S3Status = S3ConnectionStatus.Disconnected;
                    InputWindow.ChangeS3Status(S3ConnectionStatus.Disconnected);
                }
            }
            catch (Exception e)
            {
                S3Status = S3ConnectionStatus.Error;
                InputWindow.ChangeS3Status(S3ConnectionStatus.Error);
                log.Error(e);
            }
        }

        public static async Task<string> PutGIF(string path, string filename)
        {
            PutObjectRequest request = new PutObjectRequest
            {
                FilePath = @path,
                BucketName = S3_Credentials.bucketName,
                DisablePayloadSigning = true
            };

            PutObjectResponse response = await s3Client.PutObjectAsync(request);

            return S3_Credentials.bucketURL + ((S3_Credentials?.isSpecificKey == true) ? filename : $"{S3_Credentials.bucketName}/{filename}");
        }
    }
}
