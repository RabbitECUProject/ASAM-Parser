# ASAM-Parser
A tool that parses source code ASAM source code comments, along with MAP and LST files to out ASAM A2L file.

# Usage
The compiled tool must reside in the location "C:\MDAC\ECUHOST\Dev Tools", alongside the resource file "ASAM Tool.ini".

Here is an example of the resource ini file:

[Paths]
Source=C:\Users\mdaut\Documents\TEST_CLONES\RE
Linker=C:\Users\mdaut\Documents\TEST_CLONES\RE\Debug
SRecord=C:\Users\mdaut\Documents\TEST_CLONES\RE\Debug\ECUHostMKS20ECUHostMCUXpresso.s19
UserCalPatch=C:\Users\mdaut\Documents\TEST_CLONES\RE\Source\Client\usercal.h
[Structures]
CalStruct=USERCAL_stRAMCAL

You can customise the paths to the project you are working on here to make the selection of paths and files faster when running the ASAM Parser. The parser examines the complete souce code for //ASAM comment lines such as this:

EXTERN GPM6_ttVolts BVM_tBattVolts;
//ASAM mode=readvalue name="Battery Voltage" type=uint32 offset=0 min=0 max=20 m=0.001 b=0 units="V" format=5.3 help="Battery Voltage"

Note that the ASAM comment line pertains to the variable directly preceeding the comment line. In this example, the "Battery Voltage" ASAM A2L variable name follows the variable definition "EXTERN GPM6_ttVolts BVM_tBattVolts". The variable type is uint32, units is volts and the scaling of the raw integer is millivolts (0.001).

After running the ASAM Parser, an ASAM A2L record will be generated as shown below:

/begin MEASUREMENT Battery Voltage
"Battery Voltage"
VALUE
0x20005244
RL_VALU32
CM_BATTERY_VOLTAGE
BVM
0
20
/end MEASUREMENT

The parser uses the MAP file to locate the address of the variable. This along with the ASAM comment line is the information required to generate the complete A2L record for the variable.