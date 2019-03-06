#!/usr/bin/env bash

target=$1
out_file=$2
threads=$3
branchname=$4

function export_home() {
    local home_path=""
    if command -v cygpath >/dev/null 2>&1; then
        home_path=$(cygpath -m "$2")
    else
        home_path="$2"
    fi

    export $1_HOME=$home_path

    # Update .bashrc file
    local s_token=$1_HOME=
    if grep -q "$s_token" ~/.bashrc; then
        sed -i -E "s@$s_token.*@$s_token$home_path@" ~/.bashrc
    else
        echo "export $1_HOME=$home_path" >> ~/.bashrc
    fi
}

export FSTAR_HOME=$(pwd)/FStar
export Z3_HOME=$(pwd)/everest/z3

export_home FSTAR "$(pwd)/FStar"
export_home Z3 "$(pwd)/everest/z3"

# Add ssh identity
eval $(ssh-agent)
ssh-add .ssh/id_rsa

eval $(opam config env)

if [ "$TARGET" != "LOCAL" ] ;
then 

echo $(date -u "+%Y-%m-%d %H:%M:%S") >> $out_file

tail -f $out_file &
tail_pd=$!
{ { { { { { stdbuf -e0 -o0 ./build.sh "$@" ; } 3>&1 1>&2 2>&3 ; } | sed -u 's!^![STDERR]!' ; } 3>&1 1>&2 2>&3 ; } | sed -u 's!^![STDOUT]!' ; } 2>&1 ; } >> $out_file
kill $tail_pd

echo $(date -u "+%Y-%m-%d %H:%M:%S") >> $out_file

fi

eval $(ssh-agent)
ssh-add -D

# Generate query-stats.
# List the hints that fail to replay.
#FStar/.scripts/query-stats.py -f $out_file -F html -o log_no_replay.html -n all '--filter=fstar_usedhints=+' '--filter=fstar_tag=-' -g

# Worst offenders (longest times)
#FStar/.scripts/query-stats.py -f $out_file -F html -o log_worst.html -c -g -n 10


