using Grpc.Core;
using TeamHub.GrpcContracts.Organization.V1;
using team_hub_organization.Grpc;
using team_hub_organization.Services.Members.AllMembers;
using team_hub_organization.Services.Rbac;
using team_hub_organization.Tests.Controllers;

namespace team_hub_organization.Tests.Grpc;

public class OrganizationMemberGrpcServiceTests
{
    [Fact]
    public async Task ListMembers_ReturnsMappedMembers()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId);
        var service = new OrganizationMemberGrpcService(
            new MemberService(db, new OrganizationAuthorizationService(db)));

        var response = await service.ListMembers(
            new ListMembersRequest
            {
                OrganizationId = organization.Id.ToString(),
                ActorUserId = userId.ToString()
            },
            new TestServerCallContext());

        Assert.Single(response.Members);
        Assert.Equal(userId.ToString(), response.Members[0].UserId);
        Assert.NotEmpty(response.Members[0].Roles);
    }

    [Fact]
    public async Task ListMembers_WhenActorNotMember_ThrowsPermissionDenied()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, ownerId);
        var service = new OrganizationMemberGrpcService(
            new MemberService(db, new OrganizationAuthorizationService(db)));

        var ex = await Assert.ThrowsAsync<RpcException>(() => service.ListMembers(
            new ListMembersRequest
            {
                OrganizationId = organization.Id.ToString(),
                ActorUserId = Guid.NewGuid().ToString()
            },
            new TestServerCallContext()));

        Assert.Equal(StatusCode.PermissionDenied, ex.StatusCode);
    }

    sealed class TestServerCallContext : ServerCallContext
    {
        protected override string MethodCore => "ListMembers";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "peer";
        protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);
        protected override Metadata RequestHeadersCore { get; } = [];
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore { get; } = [];
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore { get; } = new AuthContext(string.Empty, new Dictionary<string, List<AuthProperty>>());

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
            throw new NotSupportedException();

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }
}
