# syntax=docker/dockerfile:1
#
# The stand's Keycloak image. Mustask.AppHost builds it (AddDockerfile) instead of pulling the bare image.
# Since tech-025 (ADR-0034) it carries no provider: the user projection is filled from the sign-in, not by
# a Keycloak event listener. What is left is the ownership of the store directory.
#
# The tag default must equal Mustask.AppHost's `Keycloak:Tag` default (KeycloakImageTests holds the two together).

ARG KEYCLOAK_IMAGE=quay.io/keycloak/keycloak
ARG KEYCLOAK_TAG=26.4

FROM ${KEYCLOAK_IMAGE}:${KEYCLOAK_TAG}
# start-dev keeps its database under data/h2. Created here, as Keycloak's own
# user, so the stand's named volume mounted on it starts with that ownership.
RUN mkdir -p /opt/keycloak/data/h2
