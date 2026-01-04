using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.Util;
using AutoMapper;
using HotelMan_HotelAdmin.Models;
using HttpMultipartParser;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using System.Text.Json;


[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
namespace HotelMan_HotelAdmin
{
    public class HotelAdmin
    {
        public async Task<APIGatewayProxyResponse> ListHotels(APIGatewayProxyRequest request)
        {
            var response = new APIGatewayProxyResponse
            {
                Headers = new Dictionary<string, string>(),
                StatusCode = 200
            };
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Headers", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "OPTIONS,GET");
            response.Headers.Add("Content-Type", "application/json");

            if (request.QueryStringParameters == null ||
            !request.QueryStringParameters.ContainsKey("token"))
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Body = JsonSerializer.Serialize(new { Error = "Token missing" });
                return response;
            }
            var token = request.QueryStringParameters["token"];
            var tokenDetails = new JwtSecurityToken(token);
            tokenDetails.Claims.FirstOrDefault(x => x.Type == "sub");
            var userId = tokenDetails.Claims.FirstOrDefault(x => x.Type == "sub")?.Value;
            var region = Environment.GetEnvironmentVariable("AWS_REGION");
            var dbClient = new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(region));
            using var dbContext = new DynamoDBContext(dbClient);

            var conditions = new List<ScanCondition>{
            new ScanCondition((nameof(Hotel.UserId)), Amazon.DynamoDBv2.DocumentModel.ScanOperator.Equal, userId)};

            var hotels = await dbContext
                .ScanAsync<Hotel>(conditions)
                .GetRemainingAsync();

            response.Body = JsonSerializer.Serialize(new
            {
                Hotels = hotels
            });

            return response;

        }
        public async Task<APIGatewayProxyResponse> AddHotel(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var response = new APIGatewayProxyResponse()
            {
                Headers = new Dictionary<string, string>(),
                StatusCode = 200,
            };
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Headers", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "OPTIONS,POST");

            var bodyContent = string.IsNullOrEmpty(request.Body)
    ? Array.Empty<byte>()
    : request.IsBase64Encoded
        ? Convert.FromBase64String(request.Body)
        : Encoding.UTF8.GetBytes(request.Body);

            using var memStream = new MemoryStream(bodyContent);
            var formData = MultipartFormDataParser.Parse(memStream);
            var hotelName = formData.GetParameterValue("hotelName");
            var hotelRating = formData.GetParameterValue("hotelRating");
            var hotelCity = formData.GetParameterValue("hotelCity");
            var hotelPrice = formData.GetParameterValue("hotelPrice");

            var file = formData.Files.FirstOrDefault();
            var fileName = file?.FileName;

            await using var fileContentStream = new MemoryStream();
            await file.Data.CopyToAsync(fileContentStream);
            fileContentStream.Position = 0;

            var userId = formData.GetParameterValue("userId");
            var idToken = formData.GetParameterValue("idToken");

            var token = new JwtSecurityToken(idToken);
            var group = token.Claims.FirstOrDefault(x => x.Type == "cognito:groups");
            if (group == null || group.Value != "Admin")
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                response.Body = JsonSerializer.Serialize(new { Error = "Unauthorized. Must Be a member of admin group" });
            }

            //pre defined default environment for all lambdas
            var region = Environment.GetEnvironmentVariable("AWS_REGION");
            var bucketName = Environment.GetEnvironmentVariable("bucketName");

            var client = new AmazonS3Client(RegionEndpoint.GetBySystemName(region));
            var dbClient = new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(region));
            try
            {
                await client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = fileName,
                    InputStream = fileContentStream,
                    AutoCloseStream = true
                });//Task<PutObjectResponse>
                var hotel = new Hotel()
                {
                    UserId = userId,
                    Id = Guid.NewGuid().ToString(),
                    Name = hotelName,
                    CityName = hotelCity,
                    Price = int.Parse(hotelPrice),
                    Rating = int.Parse(hotelRating),
                    FileName = fileName
                };
                using var dBContext = new DynamoDBContext(dbClient);
                await dBContext.SaveAsync(hotel);



                ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var mapperConfig = new MapperConfiguration(cfg =>
                {
                    cfg.CreateMap<Hotel, HotelCreatedEvent>()
                       .ForMember(dest => dest.CreationDateTime,
                                  opt => opt.MapFrom(_ => DateTime.Now));
                }, loggerFactory);
                var mapper = new Mapper(mapperConfig);

                var hotelCreatedEvent = mapper.Map<Hotel, HotelCreatedEvent>(hotel);

                var snsClient = new AmazonSimpleNotificationServiceClient();

                var publishResponse = await snsClient.PublishAsync(new PublishRequest
                {
                    TopicArn = Environment.GetEnvironmentVariable("snsTopicARN"),
                    Message = JsonSerializer.Serialize(hotelCreatedEvent)
                });
                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }


            Console.WriteLine("OK.");
            return response;

        }

    }
}
