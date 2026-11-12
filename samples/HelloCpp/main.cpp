#include <iostream>
#include <vector>
#include <numeric>

int main()
{
    std::vector<int> numbers = { 1, 2, 3, 4, 5 };
    int total = std::accumulate(numbers.begin(), numbers.end(), 0);
    std::cout << "Hello from DevStudio C++! Sum = " << total << std::endl;
    return 0;
}
