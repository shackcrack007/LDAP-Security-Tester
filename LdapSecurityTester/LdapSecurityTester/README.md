# LDAP Security Tester

## Purpose
This project exercises LDAP binds and simple search queries against a specified Domain Controller under different combinations of:
- Authentication types: Kerberos, NTLM, Negotiate
- LDAP message Signing (integrity)
- LDAP message Sealing (confidentiality)
- Optional SSL/TLS (LDAPS) if you extend it

It records pass/fail and basic details for each scenario and exports a CSV summary.

## Signing vs Sealing
- Signing adds a cryptographic integrity signature to each LDAP message. It prevents tampering or replay (MITM altering search results or modifying operations). Data is still readable unless protected by TLS or Sealing.
- Sealing encrypts (wraps) the LDAP message payload using the negotiated SASL security context (Kerberos/NTLM). It provides confidentiality without requiring TLS certificates. It also gives implicit integrity because encrypted blobs cannot be modified undetected.

Implications:
- Require Signing policy: Client must request and successfully negotiate signing or the bind fails.
- Require Sealing policy: Client must request sealing or the bind fails (in environments that enforce it).
- Using only Signing: Protects integrity, not confidentiality. Sensitive attribute values are visible on the wire.
- Using only Sealing: Data is encrypted; integrity protection also occurs, but policies may still explicitly require Signing.
- Basic (simple) binds send credentials in clear text unless you use TLS (LDAPS or StartTLS); sealing does not protect simple bind credentials.
- Recommended: Use LDAPS or StartTLS plus Signing. Use Sealing in Kerberos environments where you need encryption on port389 without certificates.

## What the Program Does
1. Parses command line arguments (DC, domain, user, password, ports).
2. Iterates test matrix toggling Signing and Sealing for each auth type.
3. Performs bind and a sample search (computers) to validate query capability.
4. Logs outcome to console and collects results.
5. Writes a timestamped CSV file with all test outcomes.
6. Reads local LDAP client integrity registry policy (Windows only) for reference.

## Requirements
- .NET9 SDK
- Network connectivity to the target Domain Controller
- Proper credentials (avoid hard-coding real production passwords) 
- For Kerberos tests: SPN / clock / domain configuration must be correct
- Windows for registry policy reading (program still runs cross?platform minus that part)

## Build
```
dotnet build
```

## Run Examples
Use current user (Kerberos/Negotiate automatically):
```
dotnet run --project LdapSecurityTester
```
Specify user and domain controller:
```
dotnet run --project LdapSecurityTester -- \
 -dc mydc.corp.contoso.com -domain CONTOSO \
 -testuser testadmin -testpassword "P@ssw0rd!" -ldapport389
```
Custom LDAPS port (extend code to enable useSsl true as needed):
```
dotnet run --project LdapSecurityTester -- -dc mydc.corp.contoso.com -ldapsport636
```

## Output
- Console: Pass/Fail per test with sample computer names
- CSV: LDAP_Test_Results_yyyyMMdd_HHmmss.csv

## Security Notes
- Do not check real credentials into source control.
- Prefer environment variables / secret managers for credentials.
- Use TLS when doing Basic binds.
- Enforce Signing/Sealing via Group Policy to mitigate credential relaying and MITM attacks.

## Extending
- Add StartTLS scenario (SessionOptions.StartTransportLayerSecurity) for port389.
- Add LDAPS tests (set useSsl = true and switch to LdapsPort).
- Add more search scopes/attributes or modify size limits.
- Integrate structured logging (ETW / Serilog) if needed.

## Troubleshooting
- Failures with Kerberos: Check clock sync, SPN, DNS, and ticket availability.
- LDAP Error81 /49: Connectivity or invalid credentials.
- Policy failures: Client did not request required Signing/Sealing.

## License
Internal / sample use. Add appropriate license if distributing.
