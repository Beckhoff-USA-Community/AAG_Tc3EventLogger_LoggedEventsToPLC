#
# Script1.ps1
#

. "C:\Users\Admin\Desktop\BAUS_AI\Standalone_SysMan\Script\MessageFilter.ps1"

AddMessageFilterClass

[EnvDTEUtils.MessageFilter]::Register() #Register

$projectPath   = "C:\Users\Admin\Documents\TcXaeShell\TwinCAT Project1\TwinCAT Project1\"
$solutionName  = "TwinCAT Project1.tsproj"
$fullPath	   = $projectPath + $solutionName   
$modifiedPrj   = "C:\temp\BAUS\NewPrj.tsproj"

# get standalone sysMan
$rm = new-object -com TcSysManagerRM
$sysman = $rm.CreateSysManager15()

# open existing project
$sysman.OpenConfiguration($fullPath)

# use of well-known AI interfaces
$io = $sysman.LookupTreeItem("TIID")
$ecMaster = $io.CreateChild("EtherCAT Master", 111)
$ek1100 = $ecMaster.CreateChild("EK1100", 9099, "", "EK1100")
$el1004 = $ek1100.CreateChild("EL1004", 9099, "", "EL1004")

# start project build to get build output
$sysman.BuildTargetPlatform("TwinCAT RT (x64)")

# set target
#$sysman.SetTargetNetId("127.0.0.1.1.1")

# activate configuration
$sysman.ActivateConfiguration()

# save new project
$sysman.SaveConfiguration($modifiedPrj)


Exit 0

[EnvDTEUtils.MessageFilter]::Revoke() 



