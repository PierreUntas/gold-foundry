# Gold Foundry — Prototype 1 (Unity)

Le premier prototype de `vision_3.md`, habillé d'après les images du document de vision.
Une seule tour verticale, une machine par étagère ; la matière tombe d'étage en étage :

```
Tuyau vert → Tapis à minerai → Piston → Four → Réservoir vitré → Carrousel de moules
          → Tapis à lingots → Presse à pièces → Coffre
```

Tout est généré par le code : formes, textures (pierre, panneaux rivetés, rayures jaune/noir, chaînes),
roches et lingots, particules, halos et **sons synthétisés**. Aucun asset à importer.
Même organisation que Fronton et Une coupe (pipeline intégré, interface IMGUI, Input classique).

## Ouvrir et jouer

1. Unity Hub → **Add → Add project from disk** → `~/apps/gold-foundry-unity`. Version : **6000.6.7f1**.
2. Ouvrir `Assets/GoldFoundry/Main.unity` (créée au premier lancement) → **Play**, vue Game en portrait.
3. iPhone : Build Profiles → iOS → Build (dossier `Builds/iOS`) → Xcode → ⌘R.

## Les micro-gestes (toucher / clic)

| Où | Effet |
|---|---|
| Tuyau vert | Lâche une poignée de minerai |
| Piston / la pile | Coup de piston tout de suite |
| Four | L'attise : fonte plus rapide, gerbe d'étincelles, coulée vers le réservoir |
| Réservoir | Ouvre la vanne (utile en AUTO : OFF) |
| Presse | Frappe le lingot qui attend (utile en AUTO : OFF) |
| **AUTO** (bas gauche) | Coupe l'automatisme : piston, coulées et presse n'obéissent plus qu'au toucher |
| **SON** (bas droite) | Coupe le son |

## Le rythme accumulation → tension → relâche

- **Minerai** (roches) : s'empile contre le piston.
- **Piston** : le tapis s'arrête (tension), recul, coup ; la pile s'effondre dans le four.
- **Four** : le minerai rougit et fond derrière la fenêtre ; voyants et lueur montent avec la chaleur.
  Quand assez de métal s'est accumulé, il coule dans le réservoir.
- **Réservoir** : la cuve vitrée se remplit, l'aiguille du manomètre monte, la vanne verse moule après moule.
- **Moules** : à chaque coulée le carrousel avance d'un cran ; le métal refroidit en avançant (orange → or)
  et le moule de bout de ligne se retourne pour lâcher son lingot.
- **Lingots** : tombent (clang), défilent en file jusqu'à la presse.
- **Presse** : BAM, le lingot s'écrase en une gerbe de pièces qui rebondissent dans le coffre (ting ting ting).
- **Coffre** : la jauge « USINE 1 » se remplit ; plein = carillon, le coffre se vide.

## Réglages

Objet **Gold Foundry** (Inspector) : `Auto`, `Ore Rate` (minerai/s), `Max Ore`, `Pile Trigger`.

## Remplacer par de vraies images

Voir **`ART.md`** et **`art_layout_guide.png`** à la racine du projet : chaque `Resources/Art/<nom>.png`
présent remplace la machine dessinée en code.

## Fichiers

```
Assets/GoldFoundry/
  Scripts/GFFactory.cs     construction de la tour, boucle, minerai, entrées, interface (IMGUI)
  Scripts/GFMachines.cs    piston, four, réservoir, carrousel, moules, lingots, presse, pièces
  Scripts/GFArt.cs         direction artistique procédurale (textures, roches, lingot) + crochet vers les images
  Scripts/GFParts.cs       minerai, tapis, liquide, filet de métal, zones tactiles
  Scripts/GFSfx.cs         synthèse audio
  Scripts/GFFx.cs          particules et tremblement de caméra
  Scripts/GFMat.cs         matériaux, halos (remplacent le bloom)
  Scripts/GFBootstrap.cs   démarre le jeu dans n'importe quelle scène
  Editor/                  réglages au premier lancement, correctifs Xcode
  Resources/GF_Lit.mat     matériau de base (shader Standard + émission, inclus dans les builds)
```

## Pas encore dans ce prototype (volontairement)

Économie dépensable, améliorations, prestige. L'argent et la jauge du coffre sont affichés,
mais rien ne s'achète encore : on valide d'abord que la chaîne est hypnotique à regarder et à toucher.
