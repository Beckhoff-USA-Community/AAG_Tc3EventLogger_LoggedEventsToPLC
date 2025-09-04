#
# Script1.ps1
#

. "C:\Users\Admin\Desktop\BAUS_AI\PowerShell\Script\MessageFilter.ps1"

AddMessageFilterClass

[EnvDTEUtils.MessageFilter]::Register() #Register

$projectPath   = "C:\temp\Training\AI_Training\Tutorials\"
$solutionName  = "Training.sln"
$fullPath	   = $projectPath + $solutionName   

# start TcXaeShell
$dte = new-object -com TcXaeShell.DTE.17.0

# silent mode
$dte.SuppressUI = $true
$dte.MainWindow.Visible = $true

# open solution
$sln = $dte.Solution
$sln.Open($fullPath)

# open the first project in the solution (assumption: only one project in sln)
$project = $sln.Projects.Item(1)
$sysManager = $project.Object

# start solution build and wait for finish (true)
$sln.solutionBuild.Build($true)

# get last build info ( !=0 --> error)
$lastBuildInfo = $sln.solutionBuild.LastBuildInfo

if($lastBuildInfo -eq 0){
	$dte.Quit()
	Exit 0
}
else{
	$dte.Quit()
	Exit 1
}


[EnvDTEUtils.MessageFilter]::Revoke() 



