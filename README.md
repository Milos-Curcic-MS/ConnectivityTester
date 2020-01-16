# Sql Managed Instance Connectivity Tester

This is a simple tool that's used for acquiring basic troubleshooting data in regard to Sql Managed Instance connectivity.<br>It will perform the following on the local machine it's run at:<br>

1. Print local network configuration
2. Attempt to resolve the hostname passed as `miHostname` command line argument
3. TCP ping the CIDR address range passed as `miSubnetRange` command line argument on port 1433
4. Calculate the average TCP 3-Way handshake time for successful pings for multiple connection attempts
5. TCP ping `miHostname` on port 3342 (this is in case user entered a hostname that resolves to a public endpoint for their Sql Managed Instance)

## Usage

`ConnectivityTester.exe <miSubnetRange> <miHostname> [-NumPings <num>]`<br>
* `NumPings` is an optional argument that states for how many connection attempts is the average connection setup time calculated, if not explicitly set the calculation will be done for 5 connection attempts.

## Acquiring miSubnetRange and miHostname parameters
<br>
<img src="extras/ManagedInstancePage.png" style="display:block; margin-left: auto; margin-right: auto;"/><br><br>
<img src="extras/VirtualNetworkPage.png" style="display:block; margin-left: auto; margin-right: auto;" height="50%"/><br><br>
<img src="extras/SubnetsPage.png" style="display:block; margin-left: auto; margin-right: auto;"/>