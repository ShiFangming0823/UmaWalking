# License and redistribution status

Local package preparation is complete; publication authorization is **not established** by this folder. No GitHub upload has been performed.

## UmaViewer upstream

The source dependency is [katboi01/UmaViewer](https://github.com/katboi01/UmaViewer), pinned to `07f82e9fa08f23c1cda3a9be3045a09372eae8bb`. Its Git-tracked root does not contain a top-level LICENSE, and no blanket license for the complete upstream application was identified in this review. A public repository and individual MIT-licensed components do not establish a redistribution license for the whole derivative application.

Before public release, obtain or confirm upstream permission for the intended source and binary redistribution. This package does not label the entire derivative as MIT or another license. The maintainer should then record the confirmed terms and choose a license only for code they have the right to license. GitHub explains the default-copyright consequence of an unlicensed repository in its [licensing documentation](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository).

The `source` directory contains the desktop overlay, not a complete copy of the upstream project or its commercial plugins. The setup tool retrieves the pinned upstream separately for local development. This separation does not resolve redistribution rights for the compiled runtime.

## Other components and media

- Preserved component notices are indexed in [Third-Party-Notices.md](Third-Party-Notices.md). Their scope remains the respective component.
- Upstream contains Dynamic Bone code. Its local ReadMe is a product description, not a general redistribution grant; confirm the applicable plugin/Asset Store rights, including use in source distributions. That plugin's source is not included here.
- Unity player libraries, packages and other bundled components remain subject to their own terms. Notice collection is not an exhaustive legal audit or permission grant.
- The startup photograph was supplied by the maintainer and remains in the current compiled app and source overlay. Its origin and public redistribution rights were not verified. Confirm permission or replace it with an image cleared for redistribution before publishing.
- Uma Musume names, characters and game resources belong to their respective rights holders. No endorsement or affiliation is claimed. No raw game-resource pack is included. Do not add extracted packs, databases or model/motion bundles to this distribution.
- The Steam `uma walk` link is an inspiration credit. Its artwork or code has not been copied into the source overlay as part of this packaging task, and that credit is not a license grant.

The final publishing decision and resolution of the above permissions remain with the maintainer. See [Release-Guide.md](Release-Guide.md) for the technical upload procedure.
