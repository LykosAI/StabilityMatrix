#!/bin/bash

print_hyperlink() {
    local url="$1"
    local text="$2"
    
    # macOS Terminal supports clickable links in the following format
    printf "\033]8;;%s\a%s\033]8;;\a" "$url" "$text"
}
