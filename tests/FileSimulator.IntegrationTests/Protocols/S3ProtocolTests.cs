using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using FileSimulator.IntegrationTests.Fixtures;
using FileSimulator.IntegrationTests.Support;
using FluentAssertions;
using Xunit;

namespace FileSimulator.IntegrationTests.Protocols;

/// <summary>
/// S3 protocol integration tests validating all MinIO/S3-compatible operations.
/// </summary>
[Collection("Simulator")]
public class S3ProtocolTests
{
    private readonly SimulatorCollectionFixture _fixture;

    public S3ProtocolTests(SimulatorCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Creates unique test content with timestamp and GUID.
    /// </summary>
    private static string CreateTestContent() => TestHelpers.CreateTestContent("S3 Test");

    /// <summary>
    /// Gets configured S3 client for MinIO.
    /// </summary>
    private async Task<(AmazonS3Client client, string bucketName)> GetS3ClientAsync()
    {
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var s3Server = connectionInfo.GetServer("S3");

        s3Server.Should().NotBeNull("S3 server must be available in connection info");

        var config = new AmazonS3Config
        {
            ServiceURL = $"http://{s3Server!.Host}:{s3Server.Port}",
            ForcePathStyle = true, // Required for MinIO
            UseHttp = true,
            AuthenticationRegion = "us-east-1"
        };

        var client = new AmazonS3Client(
            s3Server.Credentials.Username, // Access Key
            s3Server.Credentials.Password, // Secret Key
            config
        );

        var bucketName = "output"; // Default output bucket

        return (client, bucketName);
    }

    [Fact]
    public async Task S3_ListBuckets_ContainsOutputBucket()
    {
        // Arrange
        var (client, _) = await GetS3ClientAsync();

        try
        {
            // Act
            var response = await client.ListBucketsAsync();

            // Assert
            response.HttpStatusCode.Should().Be(System.Net.HttpStatusCode.OK, "ListBuckets should succeed");
            response.Buckets.Should().NotBeEmpty("At least one bucket should exist");

            var outputBucket = response.Buckets.FirstOrDefault(b => b.BucketName == "output");
            outputBucket.Should().NotBeNull("Output bucket should exist");
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task S3_Upload_PutObject_Succeeds()
    {
        // Arrange
        var (client, bucketName) = await GetS3ClientAsync();
        var fileName = TestHelpers.GenerateUniqueFileName("s3-upload");
        var content = CreateTestContent();

        try
        {
            // Act
            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = fileName,
                ContentBody = content
            };

            var response = await client.PutObjectAsync(request);

            // Assert
            response.HttpStatusCode.Should().Be(System.Net.HttpStatusCode.OK, "PutObject should succeed");

            // Verify object exists
            var headRequest = new GetObjectMetadataRequest
            {
                BucketName = bucketName,
                Key = fileName
            };
            var headResponse = await client.GetObjectMetadataAsync(headRequest);
            headResponse.HttpStatusCode.Should().Be(System.Net.HttpStatusCode.OK, "Uploaded object should exist");
        }
        finally
        {
            // Cleanup
            try
            {
                await client.DeleteObjectAsync(bucketName, fileName);
            }
            catch
            {
                // Ignore cleanup errors
            }
            client.Dispose();
        }
    }

    [Fact]
    public async Task S3_Download_GetObject_ReturnsContent()
    {
        // Arrange
        var (client, bucketName) = await GetS3ClientAsync();
        var fileName = TestHelpers.GenerateUniqueFileName("s3-download");
        var expectedContent = CreateTestContent();

        try
        {
            // Act - Upload first
            var putRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = fileName,
                ContentBody = expectedContent
            };
            await client.PutObjectAsync(putRequest);

            // Act - Download
            var getRequest = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = fileName
            };
            var getResponse = await client.GetObjectAsync(getRequest);
            getResponse.HttpStatusCode.Should().Be(System.Net.HttpStatusCode.OK, "GetObject should succeed");

            using var reader = new StreamReader(getResponse.ResponseStream);
            var actualContent = await reader.ReadToEndAsync();

            // Assert
            actualContent.Should().Be(expectedContent, "Downloaded content should match uploaded content");
        }
        finally
        {
            // Cleanup
            try
            {
                await client.DeleteObjectAsync(bucketName, fileName);
            }
            catch
            {
                // Ignore cleanup errors
            }
            client.Dispose();
        }
    }

    [Fact]
    public async Task S3_List_ReturnsUploadedObject()
    {
        // Arrange
        var (client, bucketName) = await GetS3ClientAsync();
        var fileName = TestHelpers.GenerateUniqueFileName("s3-list");
        var content = CreateTestContent();

        try
        {
            // Act - Upload
            var putRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = fileName,
                ContentBody = content
            };
            await client.PutObjectAsync(putRequest);

            // Act - List objects
            var listRequest = new ListObjectsV2Request
            {
                BucketName = bucketName,
                Prefix = "s3-list" // Filter to our test files
            };
            var listResponse = await client.ListObjectsV2Async(listRequest);

            // Assert
            listResponse.HttpStatusCode.Should().Be(System.Net.HttpStatusCode.OK, "ListObjectsV2 should succeed");
            listResponse.S3Objects.Should().NotBeEmpty("Bucket should contain objects");

            var uploadedObject = listResponse.S3Objects.FirstOrDefault(o => o.Key == fileName);
            uploadedObject.Should().NotBeNull($"Object {fileName} should appear in listing");
        }
        finally
        {
            // Cleanup
            try
            {
                await client.DeleteObjectAsync(bucketName, fileName);
            }
            catch
            {
                // Ignore cleanup errors
            }
            client.Dispose();
        }
    }

    [Fact]
    public async Task S3_Delete_RemovesObject()
    {
        // Arrange
        var (client, bucketName) = await GetS3ClientAsync();
        var fileName = TestHelpers.GenerateUniqueFileName("s3-delete");
        var content = CreateTestContent();

        try
        {
            // Act - Upload
            var putRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = fileName,
                ContentBody = content
            };
            await client.PutObjectAsync(putRequest);

            // Verify object exists
            var headRequestBefore = new GetObjectMetadataRequest
            {
                BucketName = bucketName,
                Key = fileName
            };
            var headResponseBefore = await client.GetObjectMetadataAsync(headRequestBefore);
            headResponseBefore.HttpStatusCode.Should().Be(System.Net.HttpStatusCode.OK, "Object should exist before deletion");

            // Act - Delete
            var deleteResponse = await client.DeleteObjectAsync(bucketName, fileName);
            deleteResponse.HttpStatusCode.Should().Be(System.Net.HttpStatusCode.NoContent, "DeleteObject should succeed");

            // Assert
            // Note: GetObjectMetadata throws exception when object doesn't exist
            Func<Task> act = async () => await client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = bucketName,
                Key = fileName
            });

            await act.Should().ThrowAsync<AmazonS3Exception>("Object should not exist after deletion")
                .Where(e => e.StatusCode == System.Net.HttpStatusCode.NotFound);
        }
        finally
        {
            // Cleanup (in case deletion failed)
            try
            {
                await client.DeleteObjectAsync(bucketName, fileName);
            }
            catch
            {
                // Ignore cleanup errors
            }
            client.Dispose();
        }
    }

    [Fact]
    public async Task S3_FullCycle_CRUD()
    {
        // Arrange
        var (client, bucketName) = await GetS3ClientAsync();
        var fileName = TestHelpers.GenerateUniqueFileName("s3-crud");
        var originalContent = CreateTestContent();
        var updatedContent = CreateTestContent();

        try
        {
            // CREATE
            var createRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = fileName,
                ContentBody = originalContent
            };
            var createResponse = await client.PutObjectAsync(createRequest);
            createResponse.HttpStatusCode.Should().Be(System.Net.HttpStatusCode.OK, "Object creation should succeed");

            // READ
            var readRequest1 = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = fileName
            };
            var readResponse1 = await client.GetObjectAsync(readRequest1);
            using (var reader = new StreamReader(readResponse1.ResponseStream))
            {
                var readContent1 = await reader.ReadToEndAsync();
                readContent1.Should().Be(originalContent, "Read content should match original");
            }

            // UPDATE (overwrite with new content)
            var updateRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = fileName,
                ContentBody = updatedContent
            };
            var updateResponse = await client.PutObjectAsync(updateRequest);
            updateResponse.HttpStatusCode.Should().Be(System.Net.HttpStatusCode.OK, "Object update should succeed");

            // Verify UPDATE
            var readRequest2 = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = fileName
            };
            var readResponse2 = await client.GetObjectAsync(readRequest2);
            using (var reader = new StreamReader(readResponse2.ResponseStream))
            {
                var readContent2 = await reader.ReadToEndAsync();
                readContent2.Should().Be(updatedContent, "Content should be updated");
            }

            // DELETE
            var deleteResponse = await client.DeleteObjectAsync(bucketName, fileName);
            deleteResponse.HttpStatusCode.Should().Be(System.Net.HttpStatusCode.NoContent, "Object deletion should succeed");

            // Verify DELETE
            Func<Task> act = async () => await client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = bucketName,
                Key = fileName
            });

            await act.Should().ThrowAsync<AmazonS3Exception>("Object should not exist after deletion")
                .Where(e => e.StatusCode == System.Net.HttpStatusCode.NotFound);
        }
        finally
        {
            // Cleanup
            try
            {
                await client.DeleteObjectAsync(bucketName, fileName);
            }
            catch
            {
                // Ignore cleanup errors
            }
            client.Dispose();
        }
    }
}
