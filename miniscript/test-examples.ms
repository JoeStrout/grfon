// This program tests reading of the various examples in the
// "examples" directory of the GRFON repo.  You do NOT need to
// include this file in your own MiniScript project.

import "grfon"

// Find the examples, relative to this script file
if version.hostName == "Mini Micro" then
	scriptPath = _sourceFile
else
	scriptPath = shellArgs[0]
end if
file.setdir file.child(file.parent(file.parent(scriptPath)), "examples")

parseFile = function(path)
	lines = file.readLines(path)
	if lines == null then
		print "Unable to read file: " + path
		return null
	end if
	data = lines.join(char(10))
	return grfon.parse(data)
end function

print
d = parseFile("squawk.grfon")
print d
print
print grfon.toGRFON(d)
