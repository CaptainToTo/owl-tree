#ifndef OWLTREE_SERVER_BUFFER_H
#define OWLTREE_SERVER_BUFFER_H

#include "network_buffer.h"
#include "message_buffer.h"
#include <string>
#include <sys/socket.h>
#include <netinet/in.h>
#include <stdint.h>
#include <vector>
#include <unordered_map>
#include <poll.h>
#include <stdio.h>
#include <stdlib.h>
#include <unistd.h>

namespace owltree {

class server_buffer : public network_buffer {
    private:
        struct client_info {
            public:
                client_id id;
                message_buffer buffer;
                int socket;

                client_info() {
                    id = client_id::none();
                    buffer = message_buffer();
                    socket = -1;
                }
        };

        int _max_clients;
        sockaddr_in _addr_info;
    
        int _server_socket;

        pollfd* _read_list;
        int _read_list_len;

        void add_to_read_list(int socket) {
            if (_read_list_len >= max_clients() + 1)
                throw std::invalid_argument("read list cannot fit any more sockets.");

            pollfd poll;
            poll.fd = socket;
            poll.events = POLLIN;
            _read_list[_read_list_len] = poll;
            _read_list_len++;
        }

        void remove_from_read_list(int ind) {
            if (ind < 0 || _read_list_len <= ind)
                throw std::invalid_argument("index out of range of read list.");
            
            for (int i = ind + 1; i < _read_list_len; i++) {
                _read_list[i - 1] = _read_list[i];
            }
            _read_list_len--;
        }

        void remove_from_read_list(client_info* client) {
            for (int i = 0; i < _read_list_len; i++) {
                if (_read_list[i].fd == client->socket) {
                    remove_from_read_list(i);
                    return;
                }
            }
        }

        std::unordered_map<int, client_info*> _clients_by_sock;

        bool try_get_client(int socket, client_info** out) {
            if (_clients_by_sock.find(socket) != _clients_by_sock.end()) {
                *out = _clients_by_sock[socket];
                return true;
            }
            return false;
        }

        std::unordered_map<uint32_t, client_info*> _clients_by_id;

        bool try_get_client(client_id id, client_info** out) {
            if (_clients_by_id.find(id.id()) != _clients_by_id.end()) {
                *out = _clients_by_id[id.id()];
                return true;
            }
            return false;
        }

    public:
        server_buffer(unsigned int addr, uint16_t port, int max_clients, int buffer_size) : network_buffer(addr, port, buffer_size) {
            _max_clients = max_clients;
            _read_list = new pollfd[max_clients + 1];
            _read_list_len = 0;
            _clients_by_sock = std::unordered_map<int, client_info*>();
            _clients_by_id = std::unordered_map<uint32_t, client_info*>();
            
            _addr_info.sin_family = AF_INET;
            _addr_info.sin_addr.s_addr = INADDR_ANY;
            _addr_info.sin_port = htons(port);
            _server_socket = socket(AF_INET, SOCK_STREAM, 0);
            bind(_server_socket, (struct sockaddr*)&_addr_info, sizeof(_addr_info));
            listen(_server_socket, max_clients);

            add_to_read_list(_server_socket);

            _local_id = client_id::none();
            _is_ready = true;
            on_ready(_local_id);
        }

        int max_clients() { return _max_clients; }

        void read() {
            int to_read = poll(_read_list, _read_list_len, 0);

            if (to_read <= 0) return;

            uint8_t* data = new uint8_t[buffer_size()];

            for (int i = 0; i < _read_list_len; i++) {
                pollfd socket = _read_list[i];

                if (socket.fd == _server_socket && socket.revents & POLLIN) {
                    sockaddr_in client;
                    socklen_t len = sizeof(client);
                    int client_socket = accept(_server_socket, (struct sockaddr*)&client, &len);

                    client_info* new_client = new client_info();
                    new_client->id = client_id();
                    new_client->buffer = message_buffer(buffer_size());
                    new_client->socket = client_socket;

                    _clients_by_sock.emplace(client_socket, new_client);
                    _clients_by_id.emplace(new_client->id.id(), new_client);

                    add_to_read_list(client_socket);

                    buffer_span span = new_client->buffer.get_span(client_message_length());
                    local_client_connect_encode(span, new_client->id);

                    for (auto pair : _clients_by_id) {
                        span = pair.second->buffer.get_span(client_message_length());
                        client_connect_encode(span, new_client->id);

                        span = new_client->buffer.get_span(client_message_length());
                        client_connect_encode(span, pair.first);
                    }

                    size_t buf_len;
                    uint8_t* buffer = new_client->buffer.get_buffer(&buf_len);

                    send(client_socket, buffer, buf_len, 0);
                    new_client->buffer.reset();

                } else if (socket.revents & POLLIN) {
                    int read_len = recv(socket.fd, data, buffer_size(), 0);

                    client_info* client = _clients_by_sock[socket.fd];

                    if (read_len <= 0) {
                        _clients_by_sock.erase(client->socket);
                        _clients_by_id.erase(client->id.id());
                        close(client->socket);
                        on_client_disconnected(client->id);
                        remove_from_read_list(i);

                        for (auto pair : _clients_by_sock) {
                            buffer_span span = pair.second->buffer.get_span(client_message_length());
                            client_disconnect_encode(span, client->id);
                        }

                        delete client;
                        continue;
                    }

                    int start = 0;
                    buffer_span span;
                    while (message_buffer::get_next_message(data, read_len, &start, &span)) {
                        // TODO: decode messages and place in incoming
                    }
                }
            }

            delete[] data;
        }

        void write() {
            message m;
            while (_outgoing->try_dequeue(&m)) {
                if (m.rpc == rpc_id::NETWORK_OBJECT_SPAWN) {
                    // TODO: span encode

                } else if (m.rpc == rpc_id::NETWORK_OBJECT_DESPAWN) {
                    // despawn encode

                } else if (m.callee != client_id::none()) {
                    client_info* client = nullptr;
                    if (try_get_client(m.callee, &client)) {
                        // encode message
                    }

                } else {
                    for (auto pair : _clients_by_sock) {
                        // encode message
                    }
                }
            }

            for (auto pair : _clients_by_sock) {
                size_t len;
                uint8_t* buffer = pair.second->buffer.get_buffer(&len);
                send(pair.first, buffer, len, 0);
                pair.second->buffer.reset();
            }
        }

        void disconnect() {
            auto ids = new client_id[_clients_by_id.size()];
            int ind = 0;
            for (auto pair : _clients_by_id) {
                ids[ind] = pair.first;
                ind++;
            }
            for (int i = 0; i < ind; i++) {
                disconnect(ids[i]);
            }
            delete[] ids;
            close(_server_socket);
        }

        void disconnect(client_id id) {
            client_info* client = nullptr;
            if (try_get_client(id, &client)) {
                _clients_by_sock.erase(client->socket);
                _clients_by_id.erase(client->id.id());
                close(client->socket);
                on_client_disconnected(client->id);
                remove_from_read_list(client);

                for (auto pair : _clients_by_sock) {
                    buffer_span span = pair.second->buffer.get_span(client_message_length());
                    client_disconnect_encode(span, client->id);
                }

                delete client;
            }
        }
};

}

#endif