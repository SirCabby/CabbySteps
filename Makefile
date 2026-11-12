# CabbySteps - a MelonLoader (IL2CPP, .NET 6) mod for Baby Steps, grown from Slylos's
# Baby Steps MiniMap and QOL Features (https://www.nexusmods.com/babysteps/mods/14).
#
#   make build              build build/CabbySteps.dll (Release)
#   make test               offline checks of the projection, the map data and the mod's pure logic
#   make install            build and test, install into the game, verify the installed copy
#   make install-original   put the original mod's DLL (reference/, not in the repo) back into the game
#   make il-diff            show how the build's IL differs from the original mod's DLL
#   make pois               rebuild pois.json from tools/georef (anchors, links, Map Genie's POIs)
#   make anchors            re-read anchor positions from the game's scenes (UnityPy; see README)
#   make links              re-read what the game records for each POI (UnityPy; see README)
#   make journey            re-read the ending's painted map sheets (the map) from the game (UnityPy)
#   make mapgenie MAP=...   re-measure where Map Genie's map sits on those sheets (UnityPy, Pillow)
#   make clean              remove build output
#
# The game folder is the GameDir property of src/CabbySteps.csproj: the
# usual Steam path for your OS, or what GameDir.local.props sets. Passing
# GAME_DIR='/path/to/Baby Steps' to make overrides both. PYTHON picks the
# interpreter for the georef tools (default python3).
#
# install never touches your UserData settings or saved locations. Starter files from
# package/UserData are only copied when the game has none yet, under either name: the
# mod gives files kept under its old name (BabyStepsMinimap_*) the new one itself. It
# refuses while the game is running (FORCE=1 overrides). It takes out Mods/BabyStepsMinimap.dll
# (the mod's old name, or the original mod), which would otherwise load alongside. The old
# data folder, Mods/BabyStepsMinimap/ (Map Genie's map images that older versions shipped
# included), is left where it is: nothing in it is read any more, and the mod logs that it
# can be deleted.

MOD      := CabbySteps
OLD_MOD  := BabyStepsMinimap
PROJ     := src/$(MOD).csproj
TESTS    := tests/MapTests
CONFIG   := Release
DLL      := build/$(MOD).dll
REF_DLL  := reference/$(OLD_MOD).dll
ASSETS   := package/Mods/$(MOD)
USERDATA := package/UserData
PYTHON   ?= python3

ifdef GAME_DIR
MSBUILD_GAME := -p:GameDir="$(GAME_DIR)"
endif

# Shell snippet: set $game to the game folder exactly as the build resolves it.
RESOLVE_GAME = game="$(GAME_DIR)"; \
	if [ -z "$$game" ]; then game="$$(dotnet msbuild $(PROJ) -nologo -getProperty:GameDir)"; fi; \
	if [ ! -f "$$game/MelonLoader/net6/MelonLoader.dll" ]; then \
	  echo "error: no MelonLoader install in '$$game'." >&2; \
	  echo "       Install MelonLoader into Baby Steps, or pass GAME_DIR='/path/to/Baby Steps'." >&2; \
	  exit 1; \
	fi

# Shell snippet: stop if Baby Steps is running (the [B] keeps pgrep from matching this shell).
CHECK_NOT_RUNNING = if [ -z "$(FORCE)" ] && pgrep -f '[B]abySteps\.exe' >/dev/null 2>&1; then \
	  echo "error: Baby Steps is running. Close it, then run make $@ again (or FORCE=1)." >&2; \
	  exit 1; \
	fi

# Shell snippet: stop unless the original mod's DLL is in reference/. It's Slylos's work, so the
# repo doesn't include it: Baby Steps MiniMap and QOL Features is on Nexus Mods.
CHECK_REF_DLL = if [ ! -f "$(REF_DLL)" ]; then \
	  echo "error: $(REF_DLL) isn't here. It's the original mod's DLL (Slylos's), which this repo doesn't include:" >&2; \
	  echo "       put the BabyStepsMinimap.dll from https://www.nexusmods.com/babysteps/mods/14 there." >&2; \
	  exit 1; \
	fi

.DEFAULT_GOAL := help
.PHONY: help build test install install-original il-diff pois anchors links journey mapgenie clean

help:
	@echo "CabbySteps"
	@echo
	@echo "  make build              build $(DLL) (Release)"
	@echo "  make test               offline checks of the projection, the map data and the mod's pure logic"
	@echo "  make install            build and test, install into the game, verify the installed copy"
	@echo "  make install-original   put the original mod's DLL ($(REF_DLL), not in the repo) back into the game"
	@echo "  make il-diff            show how the build's IL differs from the original mod's DLL"
	@echo "  make pois               rebuild pois.json from tools/georef (anchors, links, Map Genie's POIs)"
	@echo "  make anchors            re-read anchor positions from the game's scenes (needs UnityPy)"
	@echo "  make links              re-read what the game records for each POI (needs UnityPy)"
	@echo "  make journey            re-read the ending's painted map sheets (the map) from the game (needs UnityPy)"
	@echo "  make mapgenie MAP=...   re-measure where Map Genie's map image sits on those sheets (UnityPy, Pillow)"
	@echo "  make clean              remove build output"
	@echo
	@echo "  GAME_DIR='/path/to/Baby Steps'  use another game folder; FORCE=1  install while the game runs"
	@echo "  PYTHON=.venv/bin/python         interpreter for the georef tools"

build:
	dotnet build $(PROJ) -c $(CONFIG) -nologo $(MSBUILD_GAME)

test:
	dotnet run --project $(TESTS) -c $(CONFIG) -- .

install: build test
	@set -e; $(RESOLVE_GAME); $(CHECK_NOT_RUNNING); \
	mods="$$game/Mods"; userdata="$$game/UserData"; \
	mkdir -p "$$mods/$(MOD)" "$$userdata"; \
	if [ -e "$$mods/$(OLD_MOD).dll" ]; then \
	  rm -f "$$mods/$(OLD_MOD).dll"; \
	  echo "  removed Mods/$(OLD_MOD).dll (the mod's old name, or the original mod: it would load alongside)"; \
	fi; \
	install -m 644 "$(DLL)" "$$mods/$(MOD).dll"; \
	for f in $(ASSETS)/*; do \
	  dst="$$mods/$(MOD)/$${f##*/}"; \
	  if ! cmp -s "$$f" "$$dst"; then install -m 644 "$$f" "$$dst"; echo "  updated Mods/$(MOD)/$${f##*/}"; fi; \
	done; \
	for f in $(USERDATA)/*; do \
	  name="$${f##*/}"; dst="$$userdata/$$name"; old="$$userdata/$(OLD_MOD)$${name#$(MOD)}"; \
	  if [ ! -e "$$dst" ] && [ ! -e "$$old" ]; then install -m 644 "$$f" "$$dst"; echo "  added starter UserData/$$name"; fi; \
	done; \
	cmp "$(DLL)" "$$mods/$(MOD).dll"; \
	for f in $(ASSETS)/*; do cmp "$$f" "$$mods/$(MOD)/$${f##*/}"; done; \
	echo "installed $$mods/$(MOD).dll and Mods/$(MOD)/ (verified identical to the build)"; \
	echo "  sha256 $$(sha256sum < "$$mods/$(MOD).dll" | cut -d' ' -f1)"; \
	if [ -d "$$mods/$(OLD_MOD)" ]; then echo "  note: Mods/$(OLD_MOD)/, the old data folder, isn't read any more and can be deleted"; fi

install-original:
	@set -e; $(CHECK_REF_DLL); $(RESOLVE_GAME); $(CHECK_NOT_RUNNING); \
	rm -f "$$game/Mods/$(MOD).dll"; \
	install -m 644 "$(REF_DLL)" "$$game/Mods/$(OLD_MOD).dll"; \
	cmp "$(REF_DLL)" "$$game/Mods/$(OLD_MOD).dll"; \
	echo "installed the original mod's DLL: $$game/Mods/$(OLD_MOD).dll (and took out $(MOD).dll, which would load alongside)"; \
	echo "  note: it needs manual calibration, and Map Genie's map images (map.png, map_hi.png), which this repo doesn't ship"

il-diff: build
	@$(CHECK_REF_DLL); $(RESOLVE_GAME); \
	python3 tools/ilcompare.py "$(REF_DLL)" "$(DLL)" \
	  -r "$$game/MelonLoader/net6" -r "$$game/MelonLoader/Il2CppAssemblies"; \
	status=$$?; [ $$status -le 1 ]

pois:
	$(PYTHON) tools/georef/build_pois.py

anchors:
	@$(RESOLVE_GAME); $(PYTHON) tools/georef/find_anchors.py --game "$$game"

links:
	@$(RESOLVE_GAME); $(PYTHON) tools/georef/find_links.py --game "$$game"

journey:
	@$(RESOLVE_GAME); $(PYTHON) tools/georef/find_journey.py --game "$$game"

mapgenie:
	@if [ -z "$(MAP)" ]; then echo "usage: make mapgenie MAP=/path/to/mapgenie/map.png [PYTHON=.venv/bin/python]" >&2; exit 1; fi
	@$(RESOLVE_GAME); $(PYTHON) tools/georef/register_mapgenie.py --map "$(MAP)" --game "$$game"

clean:
	rm -rf build src/bin src/obj $(TESTS)/bin $(TESTS)/obj
