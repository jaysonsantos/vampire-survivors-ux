{
  description = "Vampire Survivors UX mod: recap buttons, double click, speed, pause label";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";

  outputs =
    { nixpkgs, ... }:
    let
      forAllSystems = nixpkgs.lib.genAttrs [ "x86_64-linux" ];
    in
    {
      devShells = forAllSystems (
        system:
        let
          pkgs = nixpkgs.legacyPackages.${system};
          dotnet = pkgs.dotnet-sdk_8;
        in
        {
          default = pkgs.mkShell {
            packages = [
              dotnet
              pkgs.ilspycmd
              pkgs.shellcheck
            ];

            DOTNET_ROOT = "${dotnet}/share/dotnet";
            DOTNET_CLI_TELEMETRY_OPTOUT = "1";
            DOTNET_NOLOGO = "1";
          };
        }
      );
    };
}
