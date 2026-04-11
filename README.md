# AWS IP Update

A Windows Try application that monitors your external IP address and updates inbound rules for an AWS EC2 security group any time the IP address changes. 

## Building

You need to have the [.net 8 sdk](https://dotnet.microsoft.com/en-us/download) installed. Run this command to create a release binary:

```
dotnet publish AwsIpUpdate -c Release -r win-x64 --self-contained
```

Create a shortcut to AwsIpUpdate.exe so the tray icon runs at startup:

```
C:\Users\YOU\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup
```

## Configuration

Choose Open Config from the tray icon context menu (it's an information icon). Edit the JSON:

```json
 {
    "awsAccessKeyId": "...",
    "awsSecretAccessKey": "...",
    "awsRegion": "us-east-1",
    "securityGroupId": "...",
    "ruleOwnerTag": "managed-by-AwsIpUpdate",
    "rules": [
      { "protocol": "tcp", "fromPort": 3389, "toPort": 3389 }
    ]
  }
```

In IAM create an access key that has the following permissions:

- ec2:UpdateTags
- ec2:DescribeSecurityGroups
- ec2:AuthorizeSecurityGroupIngress
- ec2:RevokeSecurityGroupIngress

For example with a policy added to a group added to a user and then create the access key for the user. 

You don't need to change the ruleOwnerTag unless you want to, this is used to revoke previous IP addresses before adding the new one. 

Enjoy!