import subprocess
import shlex

def run(cmd):
    return subprocess.call(shlex.split(cmd))
