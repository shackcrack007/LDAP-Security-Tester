# 1- Domain controller: LDAP server signing requirements					-	Computer\HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\NTDS\Parameters\LDAPServerIntegrity
# 2- Domain controller: LDAP server Enforce signing requirements			- 	Computer\HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\NTDS\Parameters\ldapserverenforceintegrity
## 2 overrides the first one, this was the only way we can switch to signing by default. If the Enforce setting is enabled, it ignores the other policy, if its disabled, then it uses the original policy value for the configuration
 
# Domain controller: LDAP server channel binding token requirements	    - 	Computer\HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\NTDS\Parameters\LdapEnforceChannelBinding

# Clients signing requirement                                          -   Computer\HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\ldap\LDAPClientIntegrity
# Clients encryption requirement                                          -   Computer\HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\ldap\LDAPClientConfidentiality

function Check-LDAPServerSecuritySettings {
    try {

        $LDAPSigningValue = Get-ItemProperty -Path "HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\NTDS\Parameters" -Name "LDAPServerIntegrity" 
        $LDAPChannelBindingValue = Get-ItemProperty -Path "HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\NTDS\Parameters" -Name "LdapEnforceChannelBinding" 
        $LDAPEnforceSigningValue = Get-ItemProperty -Path "HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\NTDS\Parameters" -Name "ldapserverenforceintegrity"
        
        $signingEnforceStatus = switch ($LDAPEnforceSigningValue.ldapserverenforceintegrity) {
            0 { "Disabled - relies on LDAPServerIntegrity value(INSECURE)" }
            1 { "Enabled - Signed LDAP enforced (MOST SECURED)" }
            default { "Not configured (default: Disabled)" }
        }
        
        $signingStatus = switch ($LDAPSigningValue.LDAPServerIntegrity) {
            0 { "None - Unsigned LDAP permitted (INSECURE)" }
            1 { "Negotiate - Signed LDAP preferred (MODERATE)" }
            2 { "Required - Signed LDAP enforced (MOST SECURED)" }
            default { "Not configured (default: Negotiate)" }
        }
                
        $bindingStatus = switch ($LDAPChannelBindingValue.LdapEnforceChannelBinding) {
            0 { "Never - Channel Binding not enforced (INSECURE)" }
            1 { "When Supported - Enforced when supported (MODERATE)" }
            2 { "Always - Channel Binding always enforced (MOST SECURED)" }
            default { "Not configured (default: Never)" }
        }
                
        return @{
            LDAPSigning             = $signingStatus
            LDAPChannelBinding      = $bindingStatus
            LDAPSigningValue        = $LDAPSigningValue.LDAPServerIntegrity
            LDAPChannelBindingValue = $LDAPChannelBindingValue.LdapEnforceChannelBinding
            LDAPEnforceSigningValue = $LDAPEnforceSigningValue.ldapserverenforceintegrity
        }
    }
    catch {
        Write-Host "Error: $_" -ForegroundColor Red
    }
}

Write-Host "Server validation: "
Check-LDAPServerSecuritySettings | Format-Table -AutoSize

#CN=Administrator,CN=Users,DC=shked,DC=local

Write-Host "Client validation: "
# Computer\HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\ldap
# Check LDAP Client Confidentiality (Encryption requirement)
$confidentialityValue = (Get-ItemProperty -Path "HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\ldap" -Name "LDAPClientConfidentiality" -ErrorAction SilentlyContinue).LDAPClientConfidentiality
$confidentialityStatus = switch ($confidentialityValue) {
    0 { "No encryption (INSECURE)" }
    1 { "Encryption when supported (default)" }
    2 { "Required encryption (MOST SECURED)" }
    default { "Not configured (default: Encryption when supported)" }
}
Write-Host "LDAP Client Confidentiality Setting: $confidentialityStatus"

# Interpret the value
$value = (Get-ItemProperty -Path "HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\ldap" -Name "LDAPClientIntegrity" -ErrorAction SilentlyContinue).LDAPClientIntegrity
$interpretation = switch ($value) {
    0 { "None - Unsigned LDAP allowed (INSECURE)" }
    1 { "Negotiate - Signed LDAP preferred (MODERATE)" }
    2 { "Require - Signed LDAP enforced (MOST SECURED)" }
    default { "Not configured (default: Negotiate signing)" }
}
Write-Host "LDAP Client Integrity Setting: $interpretation"
