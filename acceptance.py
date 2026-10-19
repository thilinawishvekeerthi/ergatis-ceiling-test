#!/usr/bin/env python3
"""
ACCEPTANCE-1: Verify that the repository has been properly initialized.
"""

def check_acceptance_1():
    """Check that ACCEPTANCE-1 criteria are met."""
    with open('/workspace/repo/README.md', 'r') as f:
        content = f.read()
        if 'verified by ergatis' in content:
            return True
    return False

if __name__ == '__main__':
    if check_acceptance_1():
        print("ACCEPTANCE-1: PASSED")
        exit(0)
    else:
        print("ACCEPTANCE-1: FAILED")
        exit(1)
