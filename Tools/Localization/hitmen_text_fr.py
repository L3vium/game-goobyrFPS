"""French column of the translation sheet (first pass, written with the 0.10.0 sheet). The sheet is the source of truth
afterwards: edit HitmenText.csv in Excel. This only fills French cells that are still empty:
    python Tools/Localization/hitmen_text_fr.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hitmen_extract_text as sheet  # noqa: E402

FR = {
    "_LanguageName": "Français",
    # 0.11.0
    "Cheat.Revive": "Revivre (mort)", "Hitmen.NPCInfoHolstered": "{0}  -  à la ceinture",
    "Hitmen.PickpocketWeapon": "Voler {0}", "HitmenLobby.AbilityRowKey": "{0}   [{1}]",
    "HitmenMenus.ManipEntryKey": "[{1}] {0}", "HitmenMenus.MoveEntryKey": "[{1}] {0}",
    "Asset.Ability.Armor": "Armure", "Asset.Ability.Blink": "Clignement", "Asset.Ability.C4": "C4", "Asset.Ability.Charm": "Charme",
    "Asset.Ability.Dash": "Ruée", "Asset.Ability.DoubleJump": "Double saut", "Asset.Ability.Flashbang": "Grenade flash",
    "Asset.Ability.Hook": "Grappin", "Asset.Ability.Invisibility": "Invisibilité", "Asset.Ability.Jetpack": "Jetpack",
    "Asset.Ability.Lighter": "Briquet", "Asset.Ability.MultiKill": "Multi-élimination", "Asset.Ability.Pill": "Pilule",
    "Asset.Ability.PoisonVial": "Fiole de poison", "Asset.Ability.RemoteDistraction": "Distraction à distance",
    "Asset.Ability.SleepDart": "Fléchette soporifique", "Asset.Ability.SmokeGrenade": "Fumigène", "Asset.Ability.Stone": "Caillou",
    "Asset.Ability.Transposition": "Transposition", "Asset.Ability.Trap": "Piège", "Asset.Ability.WindMine": "Grenade à vent",
    "Asset.Weapon.AssaultRifle": "Fusil d'assaut", "Asset.Weapon.BlackHole": "Pistolet trou noir", "Asset.Weapon.Bow": "Arc",
    "Asset.Weapon.Goobyr": "Goobyr", "Asset.Weapon.GrenadeLauncher": "Lance-grenades", "Asset.Weapon.Minigun": "Minigun",
    "Asset.Weapon.Pistol": "Pistolet silencieux", "Asset.Weapon.RocketLauncher": "Lance-roquettes", "Asset.Weapon.SMG": "Mitraillette",
    "Asset.Weapon.Shotgun": "Fusil à pompe", "Asset.Weapon.Sniper": "Fusil de précision", "Asset.Weapon.Sword": "Couteau",
    "Hitmen.BarkSpeakerGuard": "Garde", "Hitmen.BarkSpeakerVIP": "Cible", "Hitmen.BodyContainer": "Benne à corps",
    "Hitmen.CarryBody": "Porter le corps", "Hitmen.Close": "Fermer", "Hitmen.CloseContainer": "Fermer", "Hitmen.CloseVent": "Fermer la ventilation",
    "Hitmen.CloseWindow": "Fermer la fenêtre", "Hitmen.CutPower": "Couper le courant", "Hitmen.DisableCameras": "Désactiver les caméras",
    "Hitmen.Distract": "Allumer", "Hitmen.DistractOff": "Éteindre", "Hitmen.Draw": "Aucun agent n'a survécu", "Hitmen.DropBody": "Lâcher le corps",
    "Hitmen.Eliminate": "Éliminer", "Hitmen.EnableCameras": "Activer les caméras", "Hitmen.EventCameraFeed": "Caméra",
    "Hitmen.EventDistraction": "Distraction", "Hitmen.EventTargetDown": "Cible à terre", "Hitmen.ExtractTarget": "Extraire la cible",
    "Hitmen.HackTurret": "Pirater la tourelle", "Hitmen.Hide": "Se cacher", "Hitmen.HideBody": "Cacher le corps", "Hitmen.HidingSpot": "Cachette",
    "Hitmen.HintCharm": "Charmer", "Hitmen.HintClimb": "Grimper", "Hitmen.HintClimbOver": "Passer par-dessus", "Hitmen.HintHang": "Sortir",
    "Hitmen.HintLighter": "Briquet", "Hitmen.HintMark": "Maintenir pour marquer", "Hitmen.HintRemoteActivate": "Activer à distance",
    "Hitmen.HintTransposition": "Échanger de place", "Hitmen.HintTrapMash": "Martelez Z Q S D pour vous libérer", "Hitmen.HintVault": "Sauter",
    "Hitmen.LeaveHidingSpot": "Sortir", "Hitmen.LightOff": "Lumière  -  éteinte", "Hitmen.LightOn": "Lumière  -  allumée",
    "Hitmen.LightSwitch": "Interrupteur", "Hitmen.LightSwitchInfo": "{0}  -  {1}", "Hitmen.LockedDoor": "{0}  [Verrouillée - Badge {1}]",
    "Hitmen.Locker": "Casier", "Hitmen.MountedGun": "Mitrailleuse", "Hitmen.NPCGuard": "Garde", "Hitmen.NPCInfoKeycard": "{0}  -  Badge {1}",
    "Hitmen.NPCInfoWeapon": "{0}  -  {1}", "Hitmen.NPCTarget": "Cible", "Hitmen.NearHidingTest": "Devant une cachette",
    "Hitmen.NoSignal": "PAS DE SIGNAL", "Hitmen.Off": "Éteint", "Hitmen.On": "Allumé", "Hitmen.Open": "Ouvrir", "Hitmen.OpenContainer": "Ouvrir",
    "Hitmen.OpenVent": "Ouvrir la ventilation", "Hitmen.OpenWindow": "Ouvrir la fenêtre", "Hitmen.PickUpWeapon": "Ramasser : {0}",
    "Hitmen.Pickpocket": "Voler le badge {0}", "Hitmen.RestorePower": "Rétablir le courant",
    "Hitmen.RetrievalBring": "{0}  -  apportez le corps de la cible ici", "Hitmen.RetrievalPoint": "Point d'extraction",
    "Hitmen.RideZipline": "Prendre la tyrolienne", "Hitmen.RoomNotSealed": "La pièce n'est pas fermée",
    "Hitmen.SearchFreeTest": "Pas pris par un autre garde qui fouille", "Hitmen.SearchSpotsGen": "Points de fouille autour du centre de recherche",
    "Hitmen.SpotBodies": "{0} / {1} {1}|plural(one=corps,other=corps)", "Hitmen.SpotFull": "pleine",
    "Hitmen.SpotInfo": "{0}  -  {1}", "Hitmen.SpotInfoFull": "{0}  -  {1}  -  {2}", "Hitmen.SpotInfoState": "{0}  -  {1}",
    "Hitmen.SpotNoRoom": "pas de place pour se cacher", "Hitmen.SpotTaken": "quelqu'un est dedans", "Hitmen.SpotTarget": "la cible ne peut pas être cachée",
    "Hitmen.SwapAbility": "Échanger contre {0}", "Hitmen.SwapWeapon": "Échanger contre {0}", "Hitmen.SwitchOff": "Éteindre",
    "Hitmen.SwitchOn": "Allumer", "Hitmen.TakeAbility": "Prendre : {0}", "Hitmen.TakeKeycard": "Prendre le badge {0}", "Hitmen.Tank": "Tank",
    "Hitmen.TargetEliminated": "Cible éliminée", "Hitmen.TargetEliminatedSub": "Son corps apparaît en jaune ; les points d'extraction en or",
    "Hitmen.TeamName": "Équipe {0}", "Hitmen.ThrowHint": "{0}  Lancer", "Hitmen.Use": "Utiliser", "Hitmen.UseFromBehind": "Utiliser par l'arrière",
    "Hitmen.UseMountedGun": "Utiliser la mitrailleuse", "Hitmen.UseVehicle": "Monter", "Hitmen.Vehicle": "Véhicule",
    "Hitmen.WinDelivered": "Le VIP a été extrait", "Hitmen.WinProtect": "Tous les intrus ont été éliminés",
    "Hitmen.ZiplineOneWay": "Prendre la tyrolienne  [Seulement depuis l'autre bout]",
    "HitmenHUD.EmptySlot": "{0}  -", "HitmenHUD.Prompt": "[F]  {0}", "HitmenHUD.Slot": "{0}{1}  {2}  {3}",
    "HitmenLobby.Attack": "Attaque", "HitmenLobby.BackToRoom": "Retour", "HitmenLobby.ChooseAgent": "Choisissez votre agent",
    "HitmenLobby.CodeLabel": "CODE DU SALON", "HitmenLobby.CreateRoom": "CRÉER UN SALON", "HitmenLobby.CreateTeam": "+  Créer une équipe",
    "HitmenLobby.Difficulty": "DIFFICULTÉ", "HitmenLobby.DropHere": "Glissez des joueurs ici", "HitmenLobby.Easy": "FACILE",
    "HitmenLobby.Empty": "Vide", "HitmenLobby.FistsChoice": "Aucune", "HitmenLobby.Hard": "DIFFICILE",
    "HitmenLobby.JoinHint": "Vos amis rejoignent avec le code du salon : Rejoindre un salon sur l'écran titre.",
    "HitmenLobby.LeaveRoom": "Quitter le salon", "HitmenLobby.LookCounter": "{0} / {1}", "HitmenLobby.ManipulationRow": "MANIPULATION",
    "HitmenLobby.ManipulationRowKey": "MANIPULATION   [{0}]", "HitmenLobby.MoveRow": "DÉPLACEMENT", "HitmenLobby.MoveRowKey": "DÉPLACEMENT   [{0}]",
    "HitmenLobby.NameHint": "Nom de code", "HitmenLobby.NoneChoice": "Aucune", "HitmenLobby.Normal": "NORMAL", "HitmenLobby.NotReady": "Pas prêt",
    "HitmenLobby.OptionsHeader": "OPTIONS", "HitmenLobby.PlayerCount": "{0} / {1}", "HitmenLobby.Players": "JOUEURS", "HitmenLobby.Protect": "Défense",
    "HitmenLobby.ProximityChat": "Chat vocal de proximité", "HitmenLobby.Randomize": "Au hasard", "HitmenLobby.Ready": "Prêt",
    "HitmenLobby.ReadyCount": "{0} / {1} prêts", "HitmenLobby.StartWatching": "Commencer", "HitmenLobby.Unassigned": "SANS ÉQUIPE",
    "HitmenLobby.WaitingHost": "En attente de l'hôte...", "HitmenLobby.WatcherWait": "EN ATTENTE DES AUTRES JOUEURS\n{0} / {1} prêts",
    "HitmenLobby.WeaponRow": "ARME", "HitmenLobby.YourAgent": "VOTRE AGENT",
    "HitmenMenus.Abilities": "CAPACITÉS", "HitmenMenus.BackToLobbyEntry": "Retour au salon", "HitmenMenus.BackToLobbyHint": "Retour au salon",
    "HitmenMenus.Bow": "Révérence", "HitmenMenus.Dance": "Danse", "HitmenMenus.Debug": "DÉBOGAGE", "HitmenMenus.DebugTools": "Outils de débogage",
    "HitmenMenus.Draw": "ÉGALITÉ", "HitmenMenus.God": "Activer / désactiver le mode dieu", "HitmenMenus.Keycard": "Badge {0}",
    "HitmenMenus.Keycards": "BADGES", "HitmenMenus.Leave": "Quitter la partie", "HitmenMenus.Legend": "LÉGENDE",
    "HitmenMenus.LegendNPCs": "Gardes", "HitmenMenus.LegendTeam": "Coéquipiers", "HitmenMenus.LegendVIP": "Cible (VIP)",
    "HitmenMenus.LegendYou": "Vous", "HitmenMenus.ManipEntry": "[A] {0}", "HitmenMenus.MoveEntry": "[E] {0}", "HitmenMenus.Options": "Options",
    "HitmenMenus.Paused": "PAUSE", "HitmenMenus.PlayerWins": "{0} GAGNE", "HitmenMenus.Point": "Pointer", "HitmenMenus.Resume": "Reprendre",
    "HitmenMenus.TeamWins": "{0} GAGNE", "HitmenMenus.WaitingForHost": "En attente du retour au salon par l'hôte",
    "HitmenMenus.Wave": "Saluer", "HitmenMenus.Weapons": "ARMES",
    "HitmenOptions.AimSensitivity": "Sensibilité en visée",
    "HitmenOptions.AimSensitivityDesc": "Multiplie la sensibilité en visée. Le zoom est déjà compensé : 1,00 donne la même sensation qu'à la hanche.",
    "HitmenOptions.AntiAliasing": "Anticrénelage", "HitmenOptions.AntiAliasingDesc": "Adoucit les contours (qualité TSR).",
    "HitmenOptions.AutoDetect": "Détecter les meilleurs réglages",
    "HitmenOptions.AutoDetectDesc": "Mesure cet ordinateur (une ou deux secondes) et choisit les qualités. Se lance tout seul au premier démarrage.",
    "HitmenOptions.Back": "Retour", "HitmenOptions.Borderless": "Fenêtré sans bordure", "HitmenOptions.Camera": "Caméra",
    "HitmenOptions.ClimbMode": "Échelles et gouttières",
    "HitmenOptions.ClimbModeDesc": "Avancer grimpe vers où vous regardez (face à l'échelle : monter, dos à elle : descendre), ou avancer monte toujours.",
    "HitmenOptions.ConflictDesc": "{0}\n\nCette touche sert aussi à {1}.", "HitmenOptions.CrouchMode": "S'accroupir",
    "HitmenOptions.CrouchModeDesc": "Maintenir la touche pour rester accroupi, ou appuyer une fois pour basculer.",
    "HitmenOptions.Custom": "Personnalisé", "HitmenOptions.Defaults": "Par défaut", "HitmenOptions.Details": "Détails", "HitmenOptions.Display": "Affichage",
    "HitmenOptions.Effects": "Effets", "HitmenOptions.EffectsDesc": "Particules, fumée et feu.", "HitmenOptions.Epic": "Épique",
    "HitmenOptions.FOV": "Champ de vision", "HitmenOptions.FOVDesc": "Champ de vision horizontal de la caméra à la première personne.",
    "HitmenOptions.FollowCamera": "Suivre la caméra", "HitmenOptions.ForwardUp": "Avancer monte", "HitmenOptions.FrameLimit": "Limite d'images",
    "HitmenOptions.FrameLimitDesc": "Plafonne les images par seconde (moins de chaleur et de bruit).", "HitmenOptions.Fullscreen": "Plein écran",
    "HitmenOptions.GI": "Illumination globale",
    "HitmenOptions.GIDesc": "La lumière rebondit dans les pièces (Lumen). Bas et Moyen désactivent Lumen : bien plus rapide, pièces plus plates.",
    "HitmenOptions.HeadBob": "Balancement de la tête", "HitmenOptions.HeadBobDesc": "À quel point la vue oscille à chaque pas. 0 % la garde immobile.",
    "HitmenOptions.High": "Élevé", "HitmenOptions.Hold": "Maintenir", "HitmenOptions.InvertY": "Inverser l'axe vertical",
    "HitmenOptions.InvertYDesc": "Pousser la souris vers l'avant fait regarder vers le bas.",
    "HitmenOptions.KeyDesc": "Cliquez, puis appuyez sur une touche ou un bouton de la souris. Échap annule.",
    "HitmenOptions.Language": "Langue", "HitmenOptions.LanguageAuto": "Auto (système)",
    "HitmenOptions.LanguageDesc": "Tous les textes du jeu. Les traductions sont dans Content/Hitmen/Localization/HitmenText.csv.",
    "HitmenOptions.LanguageHeader": "Langue", "HitmenOptions.Low": "Bas", "HitmenOptions.Master": "Général", "HitmenOptions.MasterDesc": "Tout.",
    "HitmenOptions.Medium": "Moyen", "HitmenOptions.Mouse": "Souris", "HitmenOptions.Movement": "Déplacements", "HitmenOptions.Music": "Musique",
    "HitmenOptions.MusicDesc": "Musique des menus et des parties.", "HitmenOptions.NoBindings": "Les touches ne sont pas encore disponibles",
    "HitmenOptions.Off": "Non", "HitmenOptions.On": "Oui", "HitmenOptions.OptionsTitle": "OPTIONS", "HitmenOptions.PostProcess": "Post-traitement",
    "HitmenOptions.PostProcessDesc": "Flou lumineux, occlusion ambiante et autres effets d'écran.", "HitmenOptions.Preset": "Qualité globale",
    "HitmenOptions.PresetDesc": "Règle toutes les qualités ci-dessous d'un coup. En changer une passe la qualité globale en Personnalisé.",
    "HitmenOptions.PressKey": "APPUYEZ SUR UNE TOUCHE  (ÉCHAP ANNULE)", "HitmenOptions.Quality": "Qualité", "HitmenOptions.Reflections": "Reflets",
    "HitmenOptions.ReflectionsDesc": "Reflets sur les sols, le verre et le métal.", "HitmenOptions.RenderScale": "Échelle de rendu",
    "HitmenOptions.RenderScaleDesc": "Calcule moins de pixels et les agrandit (TSR). Plus bas : plus rapide et plus flou.",
    "HitmenOptions.Resolution": "Résolution", "HitmenOptions.ResolutionDesc": "Taille de l'image du jeu à l'écran.", "HitmenOptions.Run": "Courir",
    "HitmenOptions.SFX": "Effets", "HitmenOptions.SFXDesc": "Tirs, pas, portes, capacités.", "HitmenOptions.Sensitivity": "Sensibilité de la souris",
    "HitmenOptions.SensitivityDesc": "Vitesse à laquelle la vue tourne avec la souris.", "HitmenOptions.Shadows": "Ombres",
    "HitmenOptions.ShadowsDesc": "Netteté et portée des ombres.",
    "HitmenOptions.SpectatorKeyDesc": "Vol libre quand vous regardez ou après la partie. Utilisées seulement en spectateur : elles peuvent partager des touches avec le jeu.",
    "HitmenOptions.SprintMode": "Sprint",
    "HitmenOptions.SprintModeDesc": "Maintenir la touche pour sprinter, ou appuyer une fois pour basculer. Un appui bref fait une ruée si vous l'avez.",
    "HitmenOptions.TabAudio": "Audio", "HitmenOptions.TabControls": "Commandes", "HitmenOptions.TabGameplay": "Jeu", "HitmenOptions.TabGraphics": "Graphismes",
    "HitmenOptions.Textures": "Textures", "HitmenOptions.TexturesDesc": "Résolution des textures (mémoire vidéo).", "HitmenOptions.Toggle": "Basculer",
    "HitmenOptions.UIVolume": "Interface", "HitmenOptions.UIVolumeDesc": "Clics et survols des menus.", "HitmenOptions.Unlimited": "Illimitée",
    "HitmenOptions.VSync": "Synchronisation verticale", "HitmenOptions.VSyncDesc": "Supprime le déchirement ; ajoute un peu de latence.",
    "HitmenOptions.ViewDistance": "Distance d'affichage", "HitmenOptions.ViewDistanceDesc": "Jusqu'où les petits objets et les détails sont dessinés.",
    "HitmenOptions.Voice": "Voix", "HitmenOptions.VoiceDesc": "Voix et chat de proximité.", "HitmenOptions.Volume": "Volume",
    "HitmenOptions.WindowMode": "Fenêtre", "HitmenOptions.WindowModeDesc": "Plein écran, fenêtre sans bordure sur le bureau, ou fenêtre normale.",
    "HitmenOptions.Windowed": "Fenêtré",
    "HitmenTitle.Back": "Retour", "HitmenTitle.CodeHeader": "CODE DU SALON", "HitmenTitle.CodeHint": "ABC123",
    "HitmenTitle.CodeLength": "Un code de salon a 6 caractères.", "HitmenTitle.CodeNotFound": "Aucun salon avec ce code.",
    "HitmenTitle.CodeSearching": "Recherche du salon...", "HitmenTitle.CreateFailed": "Impossible de créer le salon.",
    "HitmenTitle.CreateRoom": "Créer un salon", "HitmenTitle.Creating": "Ouverture du salon...", "HitmenTitle.DebugGym": "Gymnase de débogage",
    "HitmenTitle.FriendInRoom": "Dans un salon  -  cliquez pour rejoindre", "HitmenTitle.FriendOffline": "Hors ligne", "HitmenTitle.FriendOnline": "En ligne",
    "HitmenTitle.FriendPlaying": "Joue à Hitmen", "HitmenTitle.FriendTag": "  -  ami", "HitmenTitle.Friends": "AMIS STEAM",
    "HitmenTitle.Join": "Rejoindre un salon", "HitmenTitle.JoinCode": "Rejoindre", "HitmenTitle.JoinFailed": "Impossible de rejoindre le salon.",
    "HitmenTitle.JoinTitle": "REJOINDRE UN SALON", "HitmenTitle.Joining": "Connexion...", "HitmenTitle.Logo": "HITMEN",
    "HitmenTitle.NoFriends": "Aucun ami à afficher.", "HitmenTitle.NoRooms": "Aucun salon trouvé.", "HitmenTitle.NoSteam": "Steam n'est pas lancé.",
    "HitmenTitle.Options": "Options", "HitmenTitle.Quit": "Quitter", "HitmenTitle.RoomInfo": "{0} / {1} joueurs  -  {2} ms{3}",
    "HitmenTitle.Rooms": "SALONS", "HitmenTitle.SearchFailed": "La recherche a échoué.", "HitmenTitle.Searching": "Recherche...",
    "HitmenTitle.SearchingRooms": "Recherche de salons...",
    "Hud.BodyCard": "CORPS", "Hud.DroppingIn": "LARGAGE", "Hud.Empty": "VIDE", "Hud.Exit": "SORTIR", "Hud.Fists": "POINGS", "Hud.Keycard": "BADGE {0}",
    "Hud.NoAbility": "AUCUNE", "Hud.PutDown": "{0} / molette : poser", "Hud.TargetCard": "CIBLE", "Hud.Throw": "Lancer", "Map.Target": "CIBLE",
    "Watch.Down": "[CTRL] descendre", "Watch.Faster": "[MAJ] plus vite", "Watch.FlySpeed": "VITESSE DE VOL  x{0}", "Watch.FreeFly": "VOL LIBRE",
    "Watch.Key1": "[1] vue joueur", "Watch.Key2": "[2] écran partagé", "Watch.Key3": "[3] vol libre", "Watch.Move": "se déplacer",
    "Watch.Person": "[V] première / troisième personne", "Watch.PlayerView": "VUE JOUEUR", "Watch.PlayerViewOf": "VUE JOUEUR  -  {0}",
    "Watch.PlayersOff": "[N] joueurs masqués", "Watch.PlayersOn": "[N] joueurs affichés", "Watch.Scan": "[TAB] scanner",
    "Watch.Speed": "[MOLETTE] vitesse", "Watch.SplitNobody": "ÉCRAN PARTAGÉ  -  PERSONNE À REGARDER", "Watch.SplitScreen": "ÉCRAN PARTAGÉ",
    "Watch.SwitchPlayer": "[CLIC G / CLIC D] changer de joueur", "Watch.Up": "[ESPACE] monter", "Watch.WatchersOff": "[B] spectateurs masqués",
    "Watch.WatchersOn": "[B] spectateurs affichés",
    "Cheat.Title": "TRICHES", "Cheat.Invulnerable": "Invulnérable", "Cheat.InfiniteAmmo": "Munitions infinies", "Cheat.NoCooldown": "Sans recharge",
    "Cheat.InvisibleToAI": "Invisible pour l'IA", "Cheat.RespawnEnemies": "Réapparition des ennemis", "Cheat.KillEnemies": "Tuer tous les ennemis",
    "Cheat.ResetMap": "Réinitialiser la carte", "Cheat.Fly": "Voler (sans collision)",
    "Hud.UnderFire": "SOUS LE FEU", "Hud.Spotted": "REPÉRÉ", "Hud.Searching": "RECHERCHE", "Hud.Alerted": "EN ALERTE", "Hud.Suspicious": "SUSPECT",
    "Lobby.You": "VOUS", "Lobby.HostTag": "HÔTE", "Lobby.ReadyTag": "PRÊT", "Lobby.Choosing": "EN CHOIX", "Tags.Watching": "SPECTATEUR",
}


def main():
    columns, table = sheet.read_sheet()
    if "fr" not in columns:
        columns.append("fr")
        table = {k: v + [""] for k, v in table.items()}
    fr = columns.index("fr")
    table.pop("Key", None)
    filled = 0
    for key, text in FR.items():
        if key in table and not table[key][fr].strip():
            table[key][fr] = text
            filled += 1
    order = ["_LanguageName"] + sorted(k for k in table if k != "_LanguageName")
    sheet.write_sheet(columns, table, order)
    missing = [k for k, v in table.items() if not v[fr].strip()]
    print("fr: filled", filled, "missing", len(missing), missing[:20])


if __name__ == "__main__":
    main()
