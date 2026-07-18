# solution-structure Specification

## MODIFIED Requirements

### Requirement: Ports are defined in Core and bound in Web

`PrCenter.Core` SHALL define the port interfaces `IGitHubFacts` and
`ITokenVault`. Each adapter SHALL implement its port(s): `PrCenter.GitHub`
implements `IGitHubFacts`; `PrCenter.Persistence` implements `ITokenVault`.
`PrCenter.Web` SHALL register the adapter implementations against the Core port
interfaces in its DI composition root.

#### Scenario: Host resolves all ports

- **WHEN** the `PrCenter.Web` host is built (test server or app startup)
- **THEN** resolving `IGitHubFacts` and `ITokenVault` from DI succeeds and
  yields the adapter implementations

#### Scenario: Stubs fail loudly

- **WHEN** a not-yet-implemented port member on a stub adapter is invoked
- **THEN** it throws `NotImplementedException` rather than returning fake data
