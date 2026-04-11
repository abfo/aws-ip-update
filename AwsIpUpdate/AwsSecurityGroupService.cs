using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Runtime;

namespace AwsIpUpdate;

class AwsSecurityGroupService(AppConfig config)
{
    public async Task UpdateAsync(string newIp)
    {
        string cidr = $"{newIp}/32";

        var credentials = new BasicAWSCredentials(config.AwsAccessKeyId, config.AwsSecretAccessKey);
        var region      = RegionEndpoint.GetBySystemName(config.AwsRegion);

        using var client = new AmazonEC2Client(credentials, region);

        // 1. Fetch the current security group state
        var describeResponse = await client.DescribeSecurityGroupsAsync(
            new DescribeSecurityGroupsRequest { GroupIds = [config.SecurityGroupId] });

        var sg = describeResponse.SecurityGroups.FirstOrDefault()
            ?? throw new InvalidOperationException($"Security group '{config.SecurityGroupId}' not found.");

        // 2. Find rules we own (identified by RuleOwnerTag in the description)
        var toRevoke = BuildRevokeList(sg);

        // 3. Revoke old CIDRs
        if (toRevoke.Count > 0)
        {
            await client.RevokeSecurityGroupIngressAsync(new RevokeSecurityGroupIngressRequest
            {
                GroupId       = config.SecurityGroupId,
                IpPermissions = toRevoke,
            });
        }

        // 4. Authorize new CIDRs
        var toAuthorize = config.Rules.Select(r => new IpPermission
        {
            IpProtocol = r.Protocol,
            FromPort   = r.FromPort,
            ToPort     = r.ToPort,
            Ipv4Ranges = [new IpRange { CidrIp = cidr, Description = config.RuleOwnerTag }],
        }).ToList();

        await client.AuthorizeSecurityGroupIngressAsync(new AuthorizeSecurityGroupIngressRequest
        {
            GroupId       = config.SecurityGroupId,
            IpPermissions = toAuthorize,
        });
    }

    private List<IpPermission> BuildRevokeList(SecurityGroup sg)
    {
        var result = new List<IpPermission>();

        foreach (var rule in config.Rules)
        {
            // Find the existing permission that matches this rule's protocol/port
            var existingPerm = sg.IpPermissions.FirstOrDefault(p =>
                string.Equals(p.IpProtocol, rule.Protocol, StringComparison.OrdinalIgnoreCase) &&
                p.FromPort == rule.FromPort &&
                p.ToPort   == rule.ToPort);

            if (existingPerm is null) continue;

            // Collect only the IP ranges we own
            var ownedRanges = existingPerm.Ipv4Ranges
                .Where(r => r.Description == config.RuleOwnerTag)
                .ToList();

            if (ownedRanges.Count == 0) continue;

            result.Add(new IpPermission
            {
                IpProtocol = existingPerm.IpProtocol,
                FromPort   = existingPerm.FromPort,
                ToPort     = existingPerm.ToPort,
                Ipv4Ranges = ownedRanges,
            });
        }

        return result;
    }
}
