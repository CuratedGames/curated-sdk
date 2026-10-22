# Curated SDK License

Copyright (c) Margherita Roncone. All rights reserved. Licensed to Fatum Games SL.

Curated Games is operated by Fatum Games SL (Business ID: B21832985), Calle Mediodia 7,
35015, Las Palmas De Gran Canaria, Las Palmas, Spain, under license from Margherita
Roncone. Support: support@curated.games.

These license terms are an agreement between you ("Licensee") and Fatum Games SL
("Curated") for the Curated software development kit and redistributable client
libraries (the "SDK").
Please read them before downloading or using the SDK. By downloading, installing,
or using the SDK, you agree to these terms. If you do not agree, do not download
or use the SDK.

## 1. License grant

Subject to these terms, Curated grants Licensee a nonexclusive, royalty-free,
terminable, worldwide, nontransferable license to:

1. Use and locally reproduce the SDK (including source form when provided) solely
   to develop games and applications that integrate with Curated platform services
   ("Licensee Software");
2. Reproduce and distribute the SDK redistributable object-code files that Curated
   designates for redistribution (including `Curated.Sdk.dll` and accompanying
   documentation files shipped in official Unity or NuGet packages) solely as
   embedded components of Licensee Software;
3. Modify, compile, reproduce, and distribute the official engine bindings published in
   the SDK's `engines/` folder (the Unity package, Godot addon, Unreal Engine plugin, and
   RPG Maker plugin, together the "Engine Bindings"), in source or compiled form, solely
   as embedded components of Licensee Software; and
4. Implement the published Curated launch contract (the SDK's `contracts/` folder) in
   Licensee Software built with an engine or framework for which Curated does not provide
   an Engine Binding, solely so that Licensee Software can integrate with Curated
   platform services.

Curated reserves all rights not expressly granted. No other license is granted by
implication, estoppel, or otherwise.

## 2. Redistribution limits

Licensee may distribute only the redistributable object-code packages and Engine
Bindings Curated publishes for that purpose, and only together with Licensee Software.

Licensee may not:

1. Redistribute SDK source (including Engine Bindings), internal tools, headers,
   samples, or non-redistributable packages as a standalone SDK, toolkit, or competing
   middleware product;
2. Remove, alter, or obscure copyright, attribution, or license notices included
   with the SDK redistributables;
3. Sublicense the SDK on a standalone basis, or grant rights broader than this
   license;
4. Use the SDK to develop or operate a service that replaces or circumvents Curated
   platform services for store commerce, entitlements, achievements, Cloud Save,
   friends, presence, or related partner features.

## 3. Platform services; no obligation

Nothing in this license obligates Curated to provide platform services, accept
Licensee Software for distribution on Curated storefronts, or maintain any
particular feature, uptime, or compatibility level. Access to Curated services may
require a separate partner or developer agreement, account eligibility, and
compliance with Curated policies.

## 4. Reverse engineering and substitution

Licensee will not reverse engineer the SDK except to the limited extent such
restriction is prohibited by applicable law. If Licensee Software communicates with
Curated platform services for features exposed by the SDK, Licensee will do so
through the interfaces provided by the official SDK redistributables or Engine
Bindings, or through an implementation of the launch contract permitted by Section 1,
and will not develop substitute client libraries for the purpose of bypassing SDK
terms, authentication, entitlement checks, or Curated policy enforcement.

## 4a. Player session credentials

Curated platform services may hand Licensee Software a player session credential (for
example `CURATED_SESSION_TOKEN` under the launch contract). Licensee Software will use it
only to call the Curated platform service address supplied with it, and will not log,
store, display, or transmit it to any other party or service.

## 5. Trademarks and relationship

The Curated and Curated Games names, logos, and trademarks are the property of Margherita
Roncone and are licensed to Fatum Games SL. This license does not grant any right to use
them except as needed to describe factual compatibility (for example, "supports Curated").
Licensee will not suggest that Curated sponsors, endorses, or partners with
Licensee Software unless Curated has agreed in writing.

## 6. Ownership

The SDK is licensed, not sold. Margherita Roncone, Fatum Games SL, and their licensors
retain all title and ownership in the SDK and all copies. Licensee retains all rights in Licensee
Software, excluding the SDK itself.

## 7. Updates

Curated may provide updates, corrections, or new versions of the SDK at its
discretion. Updated materials become part of the SDK under these terms unless
accompanied by different terms.

## 8. Term and termination

This license begins when Licensee first downloads or uses the SDK and continues
until terminated. Curated may terminate this license immediately upon written
notice if Licensee breaches these terms, or for any reason upon reasonable notice.
Upon termination, Licensee must stop using the SDK (other than redistributables
already lawfully shipped inside previously distributed copies of Licensee Software)
and destroy unused copies of the SDK in Licensee's possession. Sections that by
their nature should survive (including ownership, disclaimer, limitation of
liability, and indemnity) will survive termination.

## 9. Disclaimer of warranties

THE SDK IS PROVIDED "AS IS" AND "AS AVAILABLE" WITHOUT WARRANTY OF ANY KIND.
CURATED DISCLAIMS ALL WARRANTIES AND CONDITIONS, EXPRESS OR IMPLIED, INCLUDING
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, TITLE, AND NON-INFRINGEMENT.

## 10. Limitation of liability

TO THE MAXIMUM EXTENT PERMITTED BY LAW, CURATED AND ITS LICENSORS WILL NOT BE
LIABLE FOR ANY INDIRECT, INCIDENTAL, SPECIAL, CONSEQUENTIAL, OR PUNITIVE DAMAGES,
OR ANY LOSS OF PROFITS, DATA, OR BUSINESS, ARISING FROM THE SDK OR THESE TERMS,
EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGES. CURATED'S TOTAL LIABILITY FOR
ALL CLAIMS RELATING TO THE SDK WILL NOT EXCEED THE AMOUNTS LICENSEE PAID TO
CURATED SPECIFICALLY FOR THE SDK IN THE TWELVE MONTHS BEFORE THE CLAIM (OR, IF
NONE, ONE HUNDRED US DOLLARS).

## 11. Indemnity

Licensee is solely responsible for Licensee Software and its creation, distribution,
and promotion. Licensee will defend, indemnify, and hold harmless Curated and its
officers, directors, employees, and agents from claims, damages, losses, and
reasonable attorneys' fees arising out of Licensee Software or Licensee's misuse of
the SDK.

## 12. General

These terms are the entire agreement regarding the SDK and supersede prior
proposals or agreements on that subject. If any provision is unenforceable, the
remainder stays in effect. Licensee may not assign this license without Curated's
prior written consent. Failure to enforce a provision is not a waiver. These terms
are governed by the laws applicable to Curated's principal place of business,
excluding conflict-of-law rules, unless mandatory local law provides otherwise.

For partner onboarding, store distribution, and account terms, see the separate
agreements Curated presents in its developer or admin portals.
