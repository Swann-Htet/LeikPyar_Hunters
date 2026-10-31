"""The login of the deploy tools to the server of Leikpyar Hunters.

It uses the SSH KEY  C:\\Users\\<you>\\.ssh\\lh_deploy  (made once; the server only knows its public half).
No password is kept in any file. If the key is missing, the password is read from the environment variable LH_SSH_PASSWORD
(only for the first setup, or to get in again if the key is lost)."""
import os

import paramiko

HOST = "152.42.217.145"
USER = "root"
KEY = os.path.join(os.path.expanduser("~"), ".ssh", "lh_deploy")


def connect():
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    if os.path.isfile(KEY):
        c.connect(HOST, username=USER, key_filename=KEY, timeout=30, look_for_keys=False, allow_agent=False)
        return c

    password = os.environ.get("LH_SSH_PASSWORD")
    if not password:
        raise SystemExit("No SSH key at %s and no LH_SSH_PASSWORD set." % KEY)
    c.connect(HOST, username=USER, password=password, timeout=30, look_for_keys=False, allow_agent=False)
    return c
