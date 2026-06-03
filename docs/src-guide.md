# FA-Core src guide

Tahle dokumentace popisuje C# cast FA-Core modu. Je psana prakticky: kde co hledat, jak spolu tridy mluvi a kam sahnout, kdyz chces pridat novou interakci, animaci nebo opravit selection boxy.

## Rychla mapa

`FACoreModSystem.cs`

- Registruje block class `FAStation` a block entity class `FAStation`.
- V `AssetsFinalize()` upravuje zakladni selection/collision boxy pro cover workstation bloky.
- Je to vstupni bod modu pro Vintage Story API.

`BlockFAStation.cs`

- Hlavni logika bloku `fa-workstation-*`.
- Resi placeni 1x2 cover stationu, multiblock proxy, breaking, drops, pick block.
- Resi selection boxy a interakce nad `*Element` zonami ze shape souboru.
- Implementuje `IMultiBlockColSelBoxes` a `IMultiBlockInteract`, aby proxy bloky umely vracet selection boxy a predavat interakce controller bloku.

`BlockEntityFACoverStation.cs`

- Stav a chovani cover stationu po polozeni.
- Drzi stav: `lidOpen`, `fuelOpen`, `fuelLit`, `fuelStack`, `liquidStack`, `immersedStack`.
- Zpracovava akce ze selection zon: otevrit viko, otevrit fuel door, vlozit/palit fuel, lit kyselinu, vlozit/vyndat item.
- Resi client animace pres block entity behavior `Animatable` pomoci reflexe.
- Resi extra render sulfuric acid hladiny v cauldronu.

`StationShapeElementReader.cs`

- Cte `*Element` elementy ze shape JSONu.
- Z jejich `from`/`to` souradnic dela `StationElementZone`.
- Otaci zony podle smeru bloku.
- Cte animacni keyframy a umi spocitat animovane selection boxy pro elementy jako `LidOpenElement` a `FuelDoorElement`.

`StationElementZone.cs`

- Male datove typy pro interakcni zony.
- `StationElementZone` = jedna logicka interakcni zona.
- `StationElementKeyframe` = box zony v konkretnim case animace.

## Assety, ktere s tim souvisi

`assets/facore/blocktypes/station.json`

- Definuje block code `fa-workstation`.
- Nastavuje `class: FAStation` a `entityClass: FAStation`.
- Definuje varianty `purpose` a `side`.
- Definuje `Multiblock` behavior pro 1x2 stanice.
- Pro cover station je dulezite, ze JSON shape ma `block/stations/coverstation`.

`assets/facore/shapes/block/stations/coverstation.json`

- Normalni visual mesh prvky jsou napr. `Lid`, `FuelDoor`, `LiquidSurface`.
- Interakcni neviditelne boxy jsou prvky s nazvem koncicim na `Element`, napr.:
  - `LidOpenElement`
  - `FuelDoorElement`
  - `FuelElement`
  - `LiquidPourElement`
  - `TableStorageElement`
- Animace jsou `lidopen` a `fuelopen`.

## Jak tece jedna interakce

1. Hrac najede na selection box.
2. Vintage Story zavola `GetSelectionBoxes()` na controller bloku nebo `MBGetSelectionBoxes()` pres multiblock proxy.
3. `BlockFAStation` vrati boxy postavene ze `StationElementZone`.
4. Hrac klikne.
5. Vintage Story zavola `OnBlockInteractStart()` nebo `MBOnBlockInteractStart()`.
6. `BlockFAStation` podle `SelectionBoxIndex` najde odpovidajici `StationElementZone`.
7. `BlockFAStation` zavola `BlockEntityFACoverStation.HandleElementInteraction(byPlayer, zone.ActionName)`.
8. `BlockEntityFACoverStation` podle `ActionName` zmeni stav, inventory nebo liquid.
9. `MarkStationDirty()` posle zmeny na klienta a obnovi mesh/animace.

## Jak funguje `*Element` system

Interakcni zona se nepridava v C# rucne. Prida se do shape JSONu jako element, jehoz jmeno konci na `Element`.

Priklad:

```json
{
  "name": "FuelDoorElement",
  "from": [16.0, 1.0, -0.3],
  "to": [28.0, 5.2, 1.1],
  "faces": {
    "north": { "enabled": false }
  }
}
```

`StationShapeElementReader` z toho udela:

- `ElementName`: `FuelDoorElement`
- `ActionName`: `FuelDoor`
- `StationBox`: orientovany `Cuboidf` v souradnicich cele stanice
- `AnimationBoxes`: seznam animovanych boxu, pokud je element napojen na animaci

`ActionName` vznikne odstranenim suffixu `Element`. Proto `FuelDoorElement` automaticky vola case `"FuelDoor"` v `HandleElementInteraction()`.

## Selection boxy a 1x2 multiblock

Cover station je logicky 1x2 blok. Controller je normalni `FAStation` block entity, druha cast je `game:multiblock-monolithic-*` proxy.

Dulezite pojmy:

- `StationBox`: souradnice boxu v prostoru cele stanice.
- `partOffset`: offset aktualni casti stanice vuci controlleru.
- `ToPartBox()`: prepocita `StationBox` do lokalnich souradnic konkretni casti.
- `ToPartOffset()`: prepocita `Vec3i offset` z `BlockMultiblock` API na nas `partOffset`.
- `GetOwnerPartOffset()`: rozhodne, ktera cast multiblocku vlastni cely `*Element` box.

Aktualni pravidlo je: jeden `*Element` patri cele jedne casti, podle stredu aktualniho boxu. Diky tomu se `LidOpenElement` nerozpadne na maly main box a velky proxy box.

Pri ladeni selection boxu:

- Pokud se box ukazuje na spatne casti, zkontroluj `GetOwnerPartOffset()`.
- Pokud je box spravne, ale klika spatnou akci, zkontroluj poradi `BuildSelectableZones()` a `SelectionBoxIndex`.
- Pokud proxy klika spatny controller, zkontroluj `GetProxyOffset()` a `ToPartOffset()`.

## Animace a staticke selection boxy

Selection boxy jsou zamerne staticke. Klikaci zony zustavaji podle `StationBox` z `*Element` prvku, i kdyz se visual element po kliknuti animuje. Diky tomu se zony po kliknuti neposouvaji a hrac porad miri na stejne misto.

Animacni data ve `StationShapeElementReader` zustavaji uzitecna jako informace o tom, ktery `*Element` patri ke kteremu visual elementu, ale `BlockFAStation.BuildSelectionBoxes()` pouziva pro hitboxy `zone.StationBox`.

Napojeni je ted natvrdo tady:

```csharp
string? animatedElementName = elementName switch
{
    "LidOpenElement" => "Lid",
    "FuelDoorElement" => "FuelDoor",
    _ => null
};
```

Kdyz chces pridat dalsi animovanou zonu:

1. Pridej visual element do shape, napr. `Valve`.
2. Pridej interakcni element, napr. `ValveElement`.
3. Pridej animaci v shape, napr. `valveopen`, ktera animuje `Valve`.
4. Pridej mapovani `ValveElement => Valve` ve `StationShapeElementReader.GetAnimatedStationBoxes()`.
5. Pridej stav a sync animace v `BlockEntityFACoverStation`.
6. Pridej case `"Valve"` v `BlockEntityFACoverStation.HandleElementInteraction()`.

## `BlockFAStation.cs` detailne

### `OnLoaded()`

Pouzije se pri nacteni block typu.

- Jen pro `purpose == cover`.
- Nacte `elementZones` ze shape pres `StationShapeElementReader`.
- Predpocita `selectableZones` a `selectionBoxes`.
- Loguje pocty do VS logu.

Pokud v logu vidis `elementZones=0`, shape nebyl nacten nebo `*Element` prvky nejsou ve shape.

### `TryPlaceBlock()`

Pro cover station v novejsim rezimu:

- Spocte proxy pozici pres `GetProxyOffset(GetSide().Code)`.
- Overi, ze proxy pozice je nahraditelna.
- Polozi controller blok pres `base.TryPlaceBlock`.
- Rucne polozi proxy `game:multiblock-monolithic-*` pres `EnsureStationProxy()`.
- Vytvori block entity pres `EnsureStationController()`.
- Oznaci controller i proxy jako dirty.

Legacy cast kodu s `main/proxy` variantami stale existuje, ale nove cover station varianty pouzivaji `UsesLegacyParts() == false`.

### `GetSelectionBoxes()`

Vraci selection boxy pro controller block.

- Najde block entity na main pozici.
- Postavi staticke selection zony podle `StationBox`.
- Vraci boxy v lokalnich souradnicich controller casti.

### `MBGetSelectionBoxes()`

Vraci selection boxy pro multiblock proxy.

- VS predava `offset` z proxy do controlleru.
- Kod ho prevadi na `partOffset`.
- Zony se filtrujou podle owner partu.
- Vraci se cele boxy ve lokalnich souradnicich proxy casti.

### `OnBlockInteractStart()` a `MBOnBlockInteractStart()`

Resi klik:

- Podle `SelectionBoxIndex` vybere `StationElementZone`.
- Na client side jen vraci `true`, aby klient vedel, ze interakce existuje.
- Na server side zavola `BlockEntityFACoverStation.HandleElementInteraction()`.

### Selection boxy pri animaci

Selection boxy zustavaji na `zone.StationBox`. Visual animace meni mesh, ne klikaci zony.

### `BuildSelectableZones()`

Vybere zony, ktere patri aktualni casti multiblocku.

Aktualne rozhoduje podle stredu statickeho `StationBox`:

```csharp
if (GetOwnerPartOffset(zone.StationBox) == partOffset)
```

To znamena, ze jeden `*Element` je jedna logicka zona, i kdyz fyzicky presahuje hranici mezi main/proxy blokem.

## `BlockEntityFACoverStation.cs` detailne

### Stav

Block entity uklada:

- `lidOpen`: otevrene/zavrene viko.
- `fuelOpen`: otevrena/zavrena dvirka.
- `fuelLit`: jestli fuel hori.
- `liquidStack`: kapalina v cauldronu.
- `immersedStack`: item ponoreny v kyseline.
- `fuelStack`: palivo ve fuel casti.

Stav se uklada pres `ToTreeAttributes()` a nacita pres `FromTreeAttributes()`.

### `HandleElementInteraction()`

Hlavni dispatcher interakci:

- `"LidOpen"` prepne `lidOpen`.
- `"FuelDoor"` prepne `fuelOpen`.
- `"Fuel"` vola fuel logiku.
- `"LiquidPour"` vola liquid logiku.
- `"TableStorage"` vola logiku vkladani/vybirani itemu.

Kdyz pridas novy `SomethingElement`, tady musis pridat `case "Something"`.

### Fuel logika

`TryInteractFuel()`:

- Prazdna ruka: vezme fuel z tray.
- Drzis zapalovac / item s `CanIgnite`: zapali fuel.
- Jinak zkusi vlozit fuel.

`IsFuel()` pouziva combustible props itemu.

### Liquid logika

`TryInteractLiquid()`:

- Prazdna ruka: vypise stav cauldronu.
- Drzis sulfuric acid source: nalije.
- Drzis liquid sink: odebere.
- Drzis obycejny item: zkusi ho ponorit.

Kyselina je ted fixne:

```csharp
private static readonly AssetLocation SulfuricAcidCode = new("game:acid-full-sulfuric");
```

### Item v cauldronu

`TryInteractCauldronItem()`:

- Prazdna ruka: vezme ponoreny item.
- Item v ruce: vlozi item, pokud je cauldron plny kyseliny.

### Animace

Block entity pouziva `Animatable` behavior pres reflexi.

Dulezite metody:

- `EnsureAnimator()`: inicializuje animator na klientovi.
- `SyncAnimations()`: synchronizuje `lidopen` a `fuelopen` podle ulozeneho stavu.
- `SetAnimationState()`: spousti nebo rewindi animaci.
- `GetAnimationProgress()`: vraci progress animace, pokud ho budes chtit pouzit pro debug nebo budouci visual logiku.

Reflexe je pouzita proto, ze anim util neni primo typove dostupny / je vnitrni API.

### Tesselace liquidu

`OnTesselation()` muze pridat vlastni mesh hladiny.

- Pokud neni sulfuric acid, mesh se neprida.
- `CreateLiquidMesh()` zjisti texturu kapaliny.
- `CreateLiquidSurfaceMesh()` vytvori pruhy hladiny podle tvaru cauldronu.
- Mesh se rotuje podle `Block.Shape.rotateY`.

Pokud liquid neni videt:

- Zkontroluj `DebugLiquid` logy.
- Zkontroluj, jestli `liquidStack` opravdu obsahuje `game:acid-full-sulfuric`.
- Zkontroluj, jestli texture position neni null.

## `StationShapeElementReader.cs` detailne

### `LoadElementZones()`

Vstup pro nacteni zon z bloku.

- Najde shape pres `ResolveShapeLocation()`.
- Nacte shape pres `Shape.TryGet()`.
- Nacte animacni transformace pres `LoadAnimationTransforms()`.
- Rekurzivne projde vsechny elementy a children.

### `CollectZones()`

Najde kazdy element, jehoz jmeno konci na `Element`.

Z `from`/`to` udela `Cuboidf`, otoci ho podle strany stanice a ulozi jako `StationElementZone`.

### Orientace boxu

Shape je modelovana pro jeden zakladni smer, ale block ma varianty `north/east/south/west`.

Tok:

- `GetStationSide()` premapuje VS side na smer, ktery odpovida tomu, jak je shape rotovana.
- `OrientBox()` otoci box v X/Z rovine.
- `OrientPoint()` dela samotnou transformaci bodu.

Kdyz selection boxy sedi pro north, ale nesedi pro east/west/south, hledej chybu tady.

### Animovane boxy

`LoadAnimationTransforms()` projde shape animace a vytahne keyframy pro visual elementy:

- `lidopen` -> `Lid`
- `fuelopen` -> `FuelDoor`

`GetAnimatedStationBoxes()` pak umi rict: interakcni element `LidOpenElement` sleduje visual element `Lid`.

`TransformCuboid()` vezme puvodni `from/to`, aplikuje offset a rotace z keyframu a vrati box.

## `StationElementZone.cs`

`StationElementZone` je jedna logicka interakce.

Pole:

- `ElementName`: jmeno ve shape, napr. `FuelDoorElement`.
- `ActionName`: `ElementName` bez suffixu `Element`, napr. `FuelDoor`.
- `StationBox`: zakladni box.
- `AnimatedStationBox`: posledni animovany box, pokud existuje.
- `AnimationBoxes`: vsechny keyframe boxy.

`StationElementKeyframe` uklada:

- `Progress`: 0..1
- `Box`: box pro dany progress

## `FACoreModSystem.cs`

`Start()` registruje:

```csharp
api.RegisterBlockClass("FAStation", typeof(BlockFAStation));
api.RegisterBlockEntityClass("FAStation", typeof(BlockEntityFACoverStation));
```

`AssetsFinalize()` upravuje selection/collision boxy pro cover workstation bloky.

Pozor: pokud se selection boxy zacnou chovat divne uz pred `BlockFAStation.GetSelectionBoxes()`, zkontroluj i tohle misto.

## Jak pridat novou neanimovanou interakcni zonu

Priklad: chces pridat klikaci zonu `Valve`.

1. Do `coverstation.json` pridej neviditelny element `ValveElement`.
2. Element musi mit `from`, `to`, `rotationOrigin` neni nutny, pokud neni animovany.
3. V `BlockEntityFACoverStation.HandleElementInteraction()` pridej:

```csharp
case "Valve":
    // tvoje logika
    MarkStationDirty();
    return;
```

4. Volitelne pridej text do `BlockFAStation.GetZoneInteractionText()`.
5. Buildni a testuj ve hre.

## Jak pridat novou animovanou interakcni zonu

Priklad: `ValveElement` ma sledovat visual element `Valve` a animaci `valveopen`.

1. Ve shape vytvor visual element `Valve`.
2. Vytvor neviditelny `ValveElement`.
3. Pridej animaci `valveopen`, ktera animuje `Valve`.
4. V `StationShapeElementReader.GetAnimatedStationBoxes()` pridej:

```csharp
"ValveElement" => "Valve",
```

5. V `BlockEntityFACoverStation` pridej stav `valveOpen`, ulozeni do tree attributes, sync animace a case v `HandleElementInteraction()`.

## Casto rozbite veci

### Interakcni zona se neukazuje

Zkontroluj:

- Jmeno konci na `Element`.
- Element je ve shape, kterou opravdu pouziva block.
- `StationShapeElementReader.LoadElementZones()` loguje spravny pocet `elementZones`.
- Owner part podle `GetOwnerPartOffset()` odpovida main/proxy casti, na kterou koukas.

### Klik jde, ale vola spatnou akci

Zkontroluj:

- Poradi zon v `elementZones`.
- `SelectionBoxIndex`.
- Jestli `BuildSelectableZones()` pro selection a interact pouziva stejne vstupy.

### Funguje north, ale ne ostatni smery

Zkontroluj:

- `GetStationSide()`.
- `OrientPoint()`.
- `GetProxyOffset()`.
- `shapebytype.rotateY` v `station.json`.

### Animace bezi, ale selection box zustava staticky

Tohle je ted spravne chovani. Selection boxy jsou fixni podle `*Element`.

### Animace nebezi

Zkontroluj:

- `station.json` ma pro cover station `entityBehaviorsByType` s `{ "name": "Animatable" }`.
- `coverstation.json` ma animaci s code `lidopen` nebo `fuelopen`.
- Animace ma `onActivityStopped: "Rewind"` a `onAnimationEnd: "Hold"`.
- `BlockEntityFACoverStation.EnsureAnimator()` najde `animUtil` a inicializuje renderer.
- `BlockEntityFACoverStation.OnTesselation()` vola `base.OnTesselation()`.

### Stav se po restartu ztrati

Zkontroluj:

- `ToTreeAttributes()`.
- `FromTreeAttributes()`.
- `ResolveItemstack()` pro item stacky.

### Zmena je v kodu, ale ne ve hre

Zkontroluj:

- `dotnet build fa-core.csproj` prosel.
- Novy `FACore.dll` je v zipu.
- Zip je zkopirovany do `C:\Users\PC\AppData\Roaming\VintagestoryData\Mods\FACore.zip`.
- Hra byla restartovana.

## Build a nasazeni

Build:

```powershell
dotnet build fa-core.csproj
```

Vystup DLL:

```text
bin\Debug\Mods\FACore.dll
```

Aktivni mod zip pro testovani:

```text
C:\Users\PC\AppData\Roaming\VintagestoryData\Mods\FACore.zip
```

## Debug tipy

Logy Vintage Story:

```text
C:\Users\PC\AppData\Roaming\VintagestoryData\Logs
```

Uzitecne hledani:

```powershell
rg -n "FACore Station|FACore CoverStation|Exception|fa-workstation-cover" "$env:APPDATA\VintagestoryData\Logs"
```

Selection box debug:

- Sleduj log z `OnLoaded()`: `elementZones`, `selectableZones`, `selectionBoxes`.
- Pri problemu s proxy si vypis `partOffset`, `offset`, `zone.ActionName`, `StationBox`, `ToPartBox()`.

Liquid debug:

- `BlockEntityFACoverStation.DebugLiquid` je ted `true`.
- Pokud bude log moc hlucny, prepni na `false`.

## Mentalni model

Nejdulezitejsi pravidlo:

Shape JSON rika, kde jsou klikaci `*Element` boxy. `StationShapeElementReader` je precte a otoci. `BlockFAStation` rozhodne, ktery part multiblocku je vlastni a predava kliky. `BlockEntityFACoverStation` meni skutecny stav stanice.

Kdyz neco nefunguje, jdi v tomhle poradi:

1. Je `*Element` ve shape?
2. Nacetl ho `StationShapeElementReader`?
3. Patri spravnemu main/proxy partu?
4. Vraci se selection box?
5. Mapuje se `SelectionBoxIndex` na spravnou zonu?
6. Ma `BlockEntityFACoverStation.HandleElementInteraction()` case pro `ActionName`?
7. Uklada se stav a vola se `MarkStationDirty()`?
