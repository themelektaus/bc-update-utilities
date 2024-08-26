Set-ExecutionPolicy Unrestricted -Scope Process
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# Funktion zum Anzeigen der Zusammenfassung der Eingaben
function ShowInputSummary {
    param(
        [string]$OldBcServerInstance,
        [string]$NewBcServerInstance,
        [string]$TenantId,
        [string]$TenantDatabase,
        [string]$ApplicationDatabase,
        [string]$DatabaseServer,
        [string]$PartnerLicense,
        [string]$AddinsFolder,
        [string]$CustomerLicense,
        [string]$aktuelleBCVersion,
        [string]$neueBCVersion
    )

    # Neues Fenster für die Zusammenfassung
    $summaryForm = New-Object System.Windows.Forms.Form
    $summaryForm.Text = 'Eingabe-Zusammenfassung'
    $summaryForm.Size = New-Object System.Drawing.Size(550, 500)

    $summaryForm.StartPosition = 'CenterScreen'

    # TextBox für die Zusammenfassung
    $summaryTextBox = New-Object System.Windows.Forms.TextBox
    $summaryTextBox.Multiline = $true
    $summaryTextBox.ScrollBars = 'Vertical'
    $summaryTextBox.Dock = 'Fill'
    $summaryTextBox.ReadOnly = $true
    $summaryTextBox.Font = New-Object System.Drawing.Font("Arial", 15)
    $summaryTextBox.Text =  "aktuelle BC Version: $aktuelleBCVersion`r`n" +
                            "auf welche BC Version wollen Sie: $neueBCVersion`r`n" +
                            "Alter BC Server Instanzname: $OldBcServerInstance`r`n" +
                            "Neuer BC Server Instanzname: $NewBcServerInstance`r`n" +
                            "Tenant ID: $TenantId`r`n" +
                            "Tenant Datenbankname: $TenantDatabase`r`n" +
                            "Application Datenbankname: $ApplicationDatabase`r`n" +
                            "Datenbankserver: $DatabaseServer`r`n" +
                            "Partner Lizenz: $PartnerLicense`r`n" +
                            "AddInns Ordner: $AddinsFolder`r`n" +
                            "Kunden Lizenz: $CustomerLicense"

    $summaryForm.Controls.Add($summaryTextBox)
    $summaryForm.ShowDialog()
}

# Datei Öffnen Dialog 
function Read-OpenFileDialog([string]$InitialDirectory, [switch]$AllowMultiSelect)
{
    #Add-Type -AssemblyName System.Windows.Forms
    $openFileDialog = New-Object System.Windows.Forms.OpenFileDialog
    $openFileDialog.initialDirectory = $InitialDirectory
    $openFileDialog.filter = "All files (*.*)| *.*"
    if ($AllowMultiSelect) { $openFileDialog.MultiSelect = $true }
    $openFileDialog.ShowDialog() > $null
    if ($allowMultiSelect) { return $openFileDialog.Filenames } else { return $openFileDialog.Filename }
}

#Read-OpenFileDialog C:\Temp -AllowMultiSelect

# Form erstellen
$form = New-Object System.Windows.Forms.Form
$form.Text = 'Business Central Upgrade Konfiguration'
$form.Size = New-Object System.Drawing.Size(1200, 200)
$form.StartPosition = 'CenterScreen'
$form.AutoSize = $true
$form.AutoScroll = $true
$form.BackColor = [System.Drawing.Color]::FromArgb(137, 207, 240)

# Panel als pseudo "Titelleiste" erstellen
$panel1 = New-Object System.Windows.Forms.Panel
$panel1.Size = New-Object System.Drawing.Size($form.Width, 70) # Höhe des Panels auf 100 setzen
$panel1.Dock = [System.Windows.Forms.DockStyle]::Top
$panel1.BackColor = [System.Drawing.Color]::white # Hintergrundfarbe des Panels

# PictureBox für das Firmenlogo erstellen
$logo = New-Object System.Windows.Forms.PictureBox
$logo.Size = New-Object System.Drawing.Size(151, 41) # Größe des Logos
$logo.Location = New-Object System.Drawing.Point(10, 10) # Position des Logos im Panel
$logo.SizeMode = [System.Windows.Forms.PictureBoxSizeMode]::StretchImage
$logo.Image = [System.Drawing.Image]::FromFile("C:\Users\MelekTaus\Desktop\Unbenannt14\Logo.png") # Pfad zum Firmenlogo

# Sicherstellen, dass das Logo angezeigt wird
$logo.BringToFront()
# PictureBox zum Panel hinzufügen
$panel1.Controls.Add($logo)

$exitprog = New-Object System.Windows.Forms.Button
$exitprog.Size = New-Object System.Drawing.Size(50, 50) # Größe des Logos
$exitproglocation = $form.right - 20
$exitprog.Location = New-Object System.Drawing.Point($exitproglocation, 10) # Position des Logos im Panel
$exitprog.Image = [System.Drawing.Image]::FromFile("C:\Users\MelekTaus\Desktop\Unbenannt14\poweroff1.png") # Pfad zum Firmenlogo
$exitprog.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
$exitprog.FlatAppearance.BorderSize = 0

$exitprog.Add_Click(
    {
        $form.Add_FormClosing(
            {
                $_.Cancel=$false
            }
        )
        $form.Close()
    }
)

# Sicherstellen, dass der Ausschalten Button im Panel angezeigt wird
$exitprog.BringToFront()
# Button zum Panel hinzufügen
$panel1.Controls.Add($exitprog)

# Panel für Inhalte, unterstützt das Scrollen
$panel = New-Object System.Windows.Forms.Panel
$panel.Size = New-Object System.Drawing.Size($form.Width, 500)
$panel.Location = New-Object System.Drawing.Point(40, 100) # Position der Eingabemaske im Panel
$panel.AutoSize = $true
$panel.AutoScroll = $true
$panel.TabIndex = 0


# Variablen für die Positionierung der Eingabefelder
$yPos = 10
$tabIndex = 10

# Funktion zum Erstellen von Eingabefeldern
function CreateInputField($panel, $labelText, $top, [ref]$tabIndex, $createBrowseButton) {
    $label = New-Object System.Windows.Forms.Label
    $labeltop = $top + 5
    $label.Location = New-Object System.Drawing.Point(0, $labeltop)
    $label.Size = New-Object System.Drawing.Size(500, 30)
    $label.Text = $labelText
    $label.Font = New-Object System.Drawing.Font("Arial", 12)
    $panel.Controls.Add($label)

    $textBox = New-Object System.Windows.Forms.TextBox
    $textBox.Location = New-Object System.Drawing.Point(550, $top)
    $textBox.Size = New-Object System.Drawing.Size(500, 30)
    $textBox.TabIndex = $tabIndex.Value
    $textBox.Font = New-Object System.Drawing.Font("Arial", 12)
    # $textBox.Anchor = 'Right'
    #$textBox.AcceptsReturn = $true
    $panel.Controls.Add($textBox)

    if ($createBrowseButton) {
        
        $Button = New-Object System.Windows.Forms.Button
        $Button.Text = "..."
        $Button.Location = New-Object System.Drawing.Point(1060, $top)
        $Button.Size = New-Object System.Drawing.Size(50, 25)
        $Button.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat

        #$Button.FlatAppearance.BorderSize = 0

        $Button.Add_Click({
            $file = Read-OpenFileDialog($textBox, $false)
            Write-Host $file
            $textBox.Text = $file
        })
        $panel.Controls.Add($Button)
    }

    $tabIndex.Value++

    return $textBox
}


# Eingabefelder erstellen
$aktuelleBCVersion = CreateInputField $panel 'aktuelle BC Version:' $yPos ([ref]$tabIndex)
$yPos += 30
$NeueBCVersion = CreateInputField $panel 'neue BC Version:' $yPos ([ref]$tabIndex)
$yPos += 30
$OldBcServerInstance = CreateInputField $panel 'Alter BC Server Instanzname:' $yPos ([ref]$tabIndex)
$yPos += 30
$NewBcServerInstance = CreateInputField $panel 'Neuer BC Server Instanzname:' $yPos ([ref]$tabIndex)
$yPos += 30
$TenantId = CreateInputField $panel 'Tenant ID:' $yPos ([ref]$tabIndex)
$yPos += 30
$TenantDatabase = CreateInputField $panel 'Name der Tenant Datenbank:' $yPos ([ref]$tabIndex)
$yPos += 30
$ApplicationDatabase = CreateInputField $panel 'Name der Applikations Datenbank:' $yPos ([ref]$tabIndex)
$yPos += 30
$DatabaseServer = CreateInputField $panel 'Name der SQL Server Instanz, wo die BC DB liegt:' $yPos ([ref]$tabIndex)
$yPos += 30
$PartnerLicense = CreateInputField $panel 'Bitte geben Sie den Pfad ein wo Ihre Partner Lizenz liegt' $yPos ([ref]$tabIndex)
$yPos += 30
$AddinsFolder = CreateInputField $panel 'Bitte geben Sie den Pfad ein wo der AddIns Ordner liegt' $yPos ([ref]$tabIndex) 'J'
$yPos += 30
$CustomerLicense = CreateInputField $panel 'Bitte geben Sie den Pfad ein wo Ihre Kunden Lizenz liegt' $yPos ([ref]$tabIndex) 'J'
$CustomerLicense.Text = "C:\Temp\Test.license"
#Browse Button Lizenz
# $ButtonCustomerLicense =
#CreateBrowseFolderButton $panel $yPos 'txtLicense'

$yPos += 80

# Upgrade-Button
$button = New-Object System.Windows.Forms.Button
$button.Location = New-Object System.Drawing.Point(10, $yPos)
$button.Size = New-Object System.Drawing.Size(200, 30)
$button.Text = 'Eingabe prüfen'
$button.TabIndex = $tabIndex.Value
$button.Add_Click({
    # Prüfe, ob alle Felder ausgefüllt sind
    $fields = @($aktuelleBCVersion, $neueBCVersion, $OldBcServerInstance, $NewBcServerInstance, $TenantId, $TenantDatabase, $ApplicationDatabase, $DatabaseServer, $PartnerLicense)
    $allFieldsFilled = $true
    foreach ($field in $fields) {
        if ([string]::IsNullOrWhiteSpace($field.Text)) {
            $allFieldsFilled = $false
            [System.Windows.Forms.MessageBox]::Show("Bitte alle Pflichtfelder ausfüllen.", "Eingabe erforderlich" , [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Exclamation)
            return
        }
    }

    if ($allFieldsFilled) {
        # Eingaben in einem neuen Fenster anzeigen
        ShowInputSummary  -aktuelleBCVersion  $aktuelleBCVersion.Text `
                          -NeueBCVersion $neueBCVersion.Text `
                          -OldBcServerInstance $OldBcServerInstance.Text `
                          -NewBcServerInstance $NewBcServerInstance.Text `
                          -TenantId $TenantId.Text `
                          -TenantDatabase $TenantDatabase.Text `
                          -ApplicationDatabase $ApplicationDatabase.Text `
                          -DatabaseServer $DatabaseServer.Text `
                          -PartnerLicense $PartnerLicense.Text `
                          -AddinsFolder $AddinsFolder.Text `
                          -CustomerLicense $CustomerLicense.Text

    }
})
# Upgrade-Button1
$button1 = New-Object System.Windows.Forms.Button
$button1.Location = New-Object System.Drawing.Point(300, $yPos)
$button1.Size = New-Object System.Drawing.Size(200, 30)
$button1.Text = 'aktuell verfübare versionen prüfen'
$button1.TabIndex = $tabIndex.Value

#Verfügbare Updates auf Microsoft Seite prüfen
$button1.Add_Click({
Start-Process "https://support.microsoft.com/de-de/topic/updateverlauf-f%C3%BCr-microsoft-dynamics-365-business-central-94543d51-59fe-13aa-89f3-cf0cf00e09fa"
})


# Upgrade-Button2
$button2 = New-Object System.Windows.Forms.Button
$button2.Location = New-Object System.Drawing.Point(500, $yPos)
$button2.Size = New-Object System.Drawing.Size(200, 30)
$button2.Text = 'compatibility matrix'
$button2.TabIndex = $tabIndex.Value


#Dynamics 365 Business Central upgrade compatibility matrix
$button2.Add_Click({
Start-Process "https://learn.microsoft.com/en-us/dynamics365/business-central/dev-itpro/upgrade/upgrade-v14-v15-compatibility"
})
$panel.Controls.Add($button)
$panel.Controls.Add($button1)
$panel.Controls.Add($button2)

# Panel zum Formular hinzufügen und Formular anzeigen
$form.Controls.Add($panel)
$form.Controls.Add($panel1)
$form.ShowDialog()