# Gas Ventilation Systems — dev commands.
# Wraps the PowerShell scripts in tools/. Run `make help` for the list.
# Every target shells out to `powershell`, so this works whether make itself is
# running under cmd, sh (Git Bash/MSYS2) or PowerShell as its own SHELL.

PS := powershell -NoProfile -ExecutionPolicy Bypass -File

SCENARIOS ?= boot,grid,pipes,vents,effects,sensor,flee,breakout,perf,balance

.PHONY: help build test package deploy play smoke textures clean unlink

help:
	@echo "Targets:"
	@echo "  make build     - dotnet build Source -c Release"
	@echo "  make test      - dotnet test Source -c Release (Core unit tests)"
	@echo "  make package   - build, then assemble dist/GasVentilationSystems"
	@echo "  make deploy    - build + package + junction mod into RimWorld/Mods"
	@echo "  make play      - deploy, then launch RimWorld interactively (separate dev profile, dev mode on)"
	@echo "  make smoke     - full automated in-game regression (needs Steam running, RimWorld closed)"
	@echo "                   override scenarios: make smoke SCENARIOS=boot,grid"
	@echo "  make textures  - regenerate the placeholder art (tools/make-placeholder-textures.ps1)"
	@echo "  make unlink    - remove the Mods-folder junctions this project created"
	@echo "  make clean     - remove build output, dist/ and the smoke-test profile"

build:
	dotnet build Source -c Release

test:
	dotnet test Source -c Release

package:
	$(PS) tools/package.ps1

deploy:
	$(PS) tools/deploy-dev.ps1

play:
	$(PS) tools/play.ps1

smoke:
	$(PS) tools/smoke-test.ps1 -Scenarios $(SCENARIOS) -TimeoutMinutes 30

textures:
	$(PS) tools/make-placeholder-textures.ps1

unlink:
	powershell -NoProfile -Command "$$mods = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods'; foreach ($$n in 'GasVentilationSystems','GasVentilationDevHarness') { $$p = Join-Path $$mods $$n; if (Test-Path $$p) { (Get-Item $$p -Force).Delete(); Write-Output \"Removed junction $$p\" } }"

clean:
	powershell -NoProfile -Command "Remove-Item -Recurse -Force -ErrorAction SilentlyContinue Source/*/bin, Source/*/obj, 1.6/Assemblies, tools/DevHarness/1.6/Assemblies, dist, tools/.smoke-profile"
