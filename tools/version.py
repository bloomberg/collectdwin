import argparse
import fileinput
import re
import sys

#
# This script gets or updates version number. Version number
# is in AssemblyInfo.cs with the following format:
# <major version>.<minor version>.<build number>.<revision>
#
# Example:
#      [assembly: AssemblyVersion("0.5.3.0")]
#      [assembly: AssemblyFileVersion("0.5.3.0")]
#

def usage(parser) :
    parser.print_help()
    sys.exit(1);

def main( ) : 
    VERSION_FILE    = 'src\CollectdWinService\Properties\AssemblyInfo.cs'
    VERSION_FORMAT  = "{0}.{1}.{2}.{3}"
    VERSION_PATTERN = '^\[assembly: AssemblyVersion\(\"(\d+).(\d+).(\d+).(\d+)\"\)\]'
    REPLACE_PATTERN = r"(^\[assembly: Assembly.*Version\(\").*(\"\)\])"
    REPLACE_FORMAT  = r"\g<1>{0}\g<2>"

    parser = argparse.ArgumentParser()
    parser.add_argument("--command", help="get|update")
    parser.add_argument("--part", help="major|minor|build|revision")
    args = parser.parse_args()

    vfile = open(VERSION_FILE)
    for line in vfile:
        m = re.match(VERSION_PATTERN, line)
        if m:
            cmajor = int(m.group(1))
            cminor = int(m.group(2))
            cbuild = int(m.group(3))
            crevision = int(m.group(4))
            cversion = VERSION_FORMAT.format(cmajor, cminor, cbuild, crevision)

    vfile.close()
    if args.command == "get" :
        print(cversion)
        sys.exit(0) 
    elif args.command != "update" :
        print("\nError: Missing or bad COMMAND\n")
        usage(parser)
    
    if args.part == "major" :
        nmajor = cmajor + 1
        nminor = 0
        nbuild = 0
        nrevision = 0
    elif args.part == "minor" :
        nmajor = cmajor
        nminor = cminor + 1
        nbuild = 0
        nrevision = 0
    elif args.part == "build" :
        nmajor = cmajor
        nminor = cminor
        nbuild = cbuild + 1
        nrevision = 0
    elif args.part == "revision" :
        nmajor = cmajor
        nminor = cminor
        nbuild = cbuild
        nrevision = crevision + 1
    else :
        print("\nError: Missing or bad PART\n")
        usage(parser)

    nversion = VERSION_FORMAT.format(nmajor, nminor, nbuild, nrevision)

    for line in fileinput.input(files=[VERSION_FILE], inplace=1, backup='.bak'):
        line = re.sub(REPLACE_PATTERN, REPLACE_FORMAT.format(nversion), line.rstrip())
        print(line)
        

if __name__ == "__main__":
  main( )


#-----------------------------------------------------------------------------
# Copyright (C) 2015 Bloomberg Finance L.P.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
# http://www.apache.org/licenses/LICENSE-2.0
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#
#------------------------------ END-OF-FILE ----------------------------------
